using System.Globalization;
using JewelleryOrderbook.Api.Domain;
using Microsoft.Data.Sqlite;

namespace JewelleryOrderbook.Api.Infrastructure;

public sealed class SqliteOptions
{
    public string Path { get; set; } = "data/orderbook.db";

    /// <summary>
    /// Passphrase for SQLCipher. Empty means the file is unencrypted, which is fine for
    /// development and not fine for a shop's laptop. Encryption requires the connection to
    /// be built on SQLCipher (see README); the PRAGMA is harmless without it.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// SQLite implementation of <see cref="IDataStore"/>. One connection per call, WAL mode,
/// foreign keys on. The schema lives in db/schema.sql and is applied on first start.
/// </summary>
public sealed class SqliteDataStore : IDataStore
{
    private readonly string _connectionString;
    private readonly string _password;
    private readonly Lock _gate = new();

    public SqliteDataStore(SqliteOptions options)
    {
        var full = System.IO.Path.GetFullPath(options.Path);
        var directory = System.IO.Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = full,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _password = options.Password ?? string.Empty;
        EnsureSchema();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrEmpty(_password)
            ? "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;"
            : $"PRAGMA key='{_password.Replace("'", "''")}'; " +
              "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();

        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = Open();

        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customer';";
        var exists = Convert.ToInt64(check.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
        if (exists)
        {
            return;
        }

        foreach (var resource in new[] { "schema.sql", "views.sql" })
        {
            using var stream = typeof(SqliteDataStore).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded resource '{resource}' is missing.");
            using var reader = new StreamReader(stream);

            using var script = connection.CreateCommand();
            script.CommandText = reader.ReadToEnd();
            script.ExecuteNonQuery();
        }
    }

    // ------------------------------------------------------------------ customers

    public IReadOnlyList<Customer> Customers
    {
        get
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM customer ORDER BY name;";
            using var reader = command.ExecuteReader();

            var list = new List<Customer>();
            while (reader.Read())
            {
                list.Add(ReadCustomer(reader));
            }

            return list;
        }
    }

    public Customer? GetCustomer(Guid id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM customer WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadCustomer(reader) : null;
    }

    public Customer AddCustomer(Customer customer)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO customer (id, name, phone, address, pan, email, gstin, credit_days, notes, is_active, created_at, updated_at)
            VALUES ($id, $name, $phone, $address, $pan, $email, $gstin, $creditDays, $notes, $active, $created, $updated);
            """;
        BindCustomer(command, customer);
        command.ExecuteNonQuery();
        return customer;
    }

    public bool UpdateCustomer(Customer customer)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE customer SET
                name = $name, phone = $phone, address = $address, pan = $pan,
                email = $email, gstin = $gstin, credit_days = $creditDays,
                notes = $notes, is_active = $active, updated_at = $updated
            WHERE id = $id;
            """;
        BindCustomer(command, customer);
        return command.ExecuteNonQuery() > 0;
    }

    private static void BindCustomer(SqliteCommand command, Customer customer)
    {
        command.Parameters.AddWithValue("$id", customer.Id.ToString());
        command.Parameters.AddWithValue("$name", customer.Name);
        command.Parameters.AddWithValue("$phone", customer.Phone);
        command.Parameters.AddWithValue("$address", customer.Address);
        command.Parameters.AddWithValue("$pan", (object?)customer.Pan ?? DBNull.Value);
        command.Parameters.AddWithValue("$email", (object?)customer.Email ?? DBNull.Value);
        command.Parameters.AddWithValue("$gstin", (object?)customer.Gstin ?? DBNull.Value);
        command.Parameters.AddWithValue("$creditDays", customer.CreditDays);
        command.Parameters.AddWithValue("$notes", (object?)customer.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$active", customer.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$created", customer.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    private static Customer ReadCustomer(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
        Name = reader.GetString(reader.GetOrdinal("name")),
        Phone = reader.GetString(reader.GetOrdinal("phone")),
        Address = reader.GetString(reader.GetOrdinal("address")),
        Pan = reader.IsDBNull(reader.GetOrdinal("pan")) ? null : reader.GetString(reader.GetOrdinal("pan")),
        Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString(reader.GetOrdinal("email")),
        Gstin = reader.IsDBNull(reader.GetOrdinal("gstin")) ? null : reader.GetString(reader.GetOrdinal("gstin")),
        CreditDays = reader.GetInt32(reader.GetOrdinal("credit_days")),
        Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
        IsActive = reader.GetInt32(reader.GetOrdinal("is_active")) == 1,
        CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at")), CultureInfo.InvariantCulture)
    };

    // ------------------------------------------------------------------ orders

    public IReadOnlyList<Order> Orders
    {
        get
        {
            using var connection = Open();
            return LoadOrders(connection, null);
        }
    }

    public Order? GetOrder(Guid id)
    {
        using var connection = Open();
        return LoadOrders(connection, id).FirstOrDefault();
    }

    private static List<Order> LoadOrders(SqliteConnection connection, Guid? id)
    {
        var orders = new List<Order>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = id is null
                ? "SELECT * FROM sales_order ORDER BY number DESC;"
                : "SELECT * FROM sales_order WHERE id = $id;";
            if (id is not null)
            {
                command.Parameters.AddWithValue("$id", id.Value.ToString());
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                orders.Add(new Order
                {
                    Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
                    Number = reader.GetInt64(reader.GetOrdinal("number")),
                    CustomerId = Guid.Parse(reader.GetString(reader.GetOrdinal("customer_id"))),
                    OrderDate = DateOnly.Parse(reader.GetString(reader.GetOrdinal("order_date")), CultureInfo.InvariantCulture),
                    ExpectedDeliveryDate = reader.IsDBNull(reader.GetOrdinal("expected_delivery_date"))
                        ? null
                        : DateOnly.Parse(reader.GetString(reader.GetOrdinal("expected_delivery_date")), CultureInfo.InvariantCulture),
                    Status = Enum.Parse<OrderStatus>(reader.GetString(reader.GetOrdinal("status"))),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    CancellationReason = reader.IsDBNull(reader.GetOrdinal("cancellation_reason"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("cancellation_reason"))
                });
            }
        }

        if (orders.Count == 0)
        {
            return orders;
        }

        var byId = orders.ToDictionary(o => o.Id);
        var lines = new List<(Guid OrderId, OrderLine Line)>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = id is null
                ? "SELECT * FROM order_line ORDER BY order_id, position;"
                : "SELECT * FROM order_line WHERE order_id = $id ORDER BY position;";
            if (id is not null)
            {
                command.Parameters.AddWithValue("$id", id.Value.ToString());
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var orderId = Guid.Parse(reader.GetString(reader.GetOrdinal("order_id")));
                lines.Add((orderId, ReadLine(reader)));
            }
        }

        if (lines.Count > 0)
        {
            var lineIndex = lines.ToDictionary(x => x.Line.Id, x => x.Line);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM line_stone ORDER BY order_line_id, position;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var lineId = Guid.Parse(reader.GetString(reader.GetOrdinal("order_line_id")));
                if (lineIndex.TryGetValue(lineId, out var line))
                {
                    line.Stones.Add(ReadStone(reader));
                }
            }

            foreach (var (orderId, line) in lines)
            {
                if (byId.TryGetValue(orderId, out var order))
                {
                    order.Lines.Add(line);
                }
            }
        }

        return orders;
    }

    private static OrderLine ReadLine(SqliteDataReader reader)
    {
        var line = new OrderLine
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            ItemName = reader.GetString(reader.GetOrdinal("item_name")),
            ItemDescription = reader.IsDBNull(reader.GetOrdinal("item_description")) ? null : reader.GetString(reader.GetOrdinal("item_description")),
            Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
            MetalType = Enum.Parse<MetalType>(reader.GetString(reader.GetOrdinal("metal_type"))),
            MetalPurity = reader.IsDBNull(reader.GetOrdinal("metal_purity")) ? null : reader.GetString(reader.GetOrdinal("metal_purity")),
            GrossWeightMg = reader.GetInt32(reader.GetOrdinal("gross_weight_mg")),
            RatePerGramPaise = reader.GetInt64(reader.GetOrdinal("rate_per_gram_paise")),
            RateDate = DateOnly.Parse(reader.GetString(reader.GetOrdinal("rate_date")), CultureInfo.InvariantCulture),
            WastagePctBp = reader.GetInt32(reader.GetOrdinal("wastage_pct_bp")),
            MakingType = Enum.Parse<MakingChargeType>(reader.GetString(reader.GetOrdinal("making_type"))),
            MakingRate = reader.GetInt64(reader.GetOrdinal("making_rate")),
            OtherChargesPaise = reader.GetInt64(reader.GetOrdinal("other_charges_paise")),
            DiscountPaise = reader.GetInt64(reader.GetOrdinal("discount_paise")),
            Tax = new TaxConfig(
                NullableInt(reader, "tax_gold_bp"),
                NullableInt(reader, "tax_wastage_bp"),
                NullableInt(reader, "tax_making_bp"),
                NullableInt(reader, "tax_stone_bp")),
            Totals = new LineTotals(
                reader.GetInt32(reader.GetOrdinal("net_weight_mg")),
                reader.GetInt32(reader.GetOrdinal("wastage_weight_mg")),
                reader.GetInt64(reader.GetOrdinal("metal_value_paise")),
                reader.GetInt64(reader.GetOrdinal("wastage_charge_paise")),
                reader.GetInt64(reader.GetOrdinal("making_charges_paise")),
                reader.GetInt64(reader.GetOrdinal("stone_value_paise")),
                reader.GetInt64(reader.GetOrdinal("other_charges_paise")),
                reader.GetInt64(reader.GetOrdinal("subtotal_paise")),
                reader.GetInt64(reader.GetOrdinal("discount_paise")),
                reader.GetInt64(reader.GetOrdinal("taxable_paise")),
                // The per-component tax breakdown is not stored as separate columns; only the
                // total is. Reconstruct it from the rates when a tax-summary report needs it.
                0, 0, 0, 0,
                reader.GetInt64(reader.GetOrdinal("tax_paise")),
                reader.GetInt64(reader.GetOrdinal("line_total_paise")))
        };

        return line;
    }

    private static int? NullableInt(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static LineStone ReadStone(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
        Category = Enum.Parse<StoneCategory>(reader.GetString(reader.GetOrdinal("category"))),
        Name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
        OriginType = reader.IsDBNull(reader.GetOrdinal("origin_type")) ? null : reader.GetString(reader.GetOrdinal("origin_type")),
        Treatment = reader.IsDBNull(reader.GetOrdinal("treatment")) ? null : reader.GetString(reader.GetOrdinal("treatment")),
        Shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? null : reader.GetString(reader.GetOrdinal("shape")),
        StoneCount = reader.GetInt32(reader.GetOrdinal("stone_count")),
        TotalCaratsMilli = reader.GetInt32(reader.GetOrdinal("total_carats_milli")),
        ColourGrade = reader.IsDBNull(reader.GetOrdinal("colour_grade")) ? null : reader.GetString(reader.GetOrdinal("colour_grade")),
        ClarityGrade = reader.IsDBNull(reader.GetOrdinal("clarity_grade")) ? null : reader.GetString(reader.GetOrdinal("clarity_grade")),
        CertificationLab = reader.IsDBNull(reader.GetOrdinal("certification_lab")) ? null : reader.GetString(reader.GetOrdinal("certification_lab")),
        CertificateNumber = reader.IsDBNull(reader.GetOrdinal("certificate_number")) ? null : reader.GetString(reader.GetOrdinal("certificate_number")),
        StoneValuePaise = reader.GetInt64(reader.GetOrdinal("stone_value_paise")),
        SettingType = reader.IsDBNull(reader.GetOrdinal("setting_type")) ? null : reader.GetString(reader.GetOrdinal("setting_type"))
    };

    public Order AddOrder(Order order)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO sales_order (id, number, customer_id, order_date, expected_delivery_date, status, notes, cancellation_reason, created_at, updated_at)
                VALUES ($id, $number, $customerId, $orderDate, $expected, $status, $notes, $reason, $created, $updated);
                """;
            command.Parameters.AddWithValue("$id", order.Id.ToString());
            command.Parameters.AddWithValue("$number", order.Number);
            command.Parameters.AddWithValue("$customerId", order.CustomerId.ToString());
            command.Parameters.AddWithValue("$orderDate", order.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$expected", order.ExpectedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$status", order.Status.ToString());
            command.Parameters.AddWithValue("$notes", (object?)order.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("$reason", (object?)order.CancellationReason ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }

        var position = 0;
        foreach (var line in order.Lines)
        {
            position++;
            InsertLine(connection, transaction, order.Id, line, position);
        }

        transaction.Commit();
        return order;
    }

    private static void InsertLine(SqliteConnection connection, SqliteTransaction transaction, Guid orderId, OrderLine line, int position)
    {
        var totals = line.Totals;

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO order_line (
                    id, order_id, position, item_name, item_description, quantity,
                    metal_type, metal_purity, gross_weight_mg, rate_per_gram_paise, rate_date, wastage_pct_bp,
                    making_type, making_rate, other_charges_paise, discount_paise,
                    tax_gold_bp, tax_wastage_bp, tax_making_bp, tax_stone_bp,
                    net_weight_mg, wastage_weight_mg, metal_value_paise, wastage_charge_paise,
                    making_charges_paise, stone_value_paise, subtotal_paise, taxable_paise, tax_paise, line_total_paise)
                VALUES (
                    $id, $orderId, $position, $itemName, $description, $quantity,
                    $metalType, $purity, $gross, $rate, $rateDate, $wastage,
                    $makingType, $makingRate, $other, $discount,
                    $taxGold, $taxWastage, $taxMaking, $taxStone,
                    $net, $wastageMg, $metalValue, $wastageCharge,
                    $makingCharges, $stoneValue, $subtotal, $taxable, $tax, $lineTotal);
                """;

            command.Parameters.AddWithValue("$id", line.Id.ToString());
            command.Parameters.AddWithValue("$orderId", orderId.ToString());
            command.Parameters.AddWithValue("$position", position);
            command.Parameters.AddWithValue("$itemName", line.ItemName);
            command.Parameters.AddWithValue("$description", (object?)line.ItemDescription ?? DBNull.Value);
            command.Parameters.AddWithValue("$quantity", line.Quantity);
            command.Parameters.AddWithValue("$metalType", line.MetalType.ToString());
            command.Parameters.AddWithValue("$purity", (object?)line.MetalPurity ?? DBNull.Value);
            command.Parameters.AddWithValue("$gross", line.GrossWeightMg);
            command.Parameters.AddWithValue("$rate", line.RatePerGramPaise);
            command.Parameters.AddWithValue("$rateDate", line.RateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$wastage", line.WastagePctBp);
            command.Parameters.AddWithValue("$makingType", line.MakingType.ToString());
            command.Parameters.AddWithValue("$makingRate", line.MakingRate);
            command.Parameters.AddWithValue("$other", line.OtherChargesPaise);
            command.Parameters.AddWithValue("$discount", line.DiscountPaise);
            command.Parameters.AddWithValue("$taxGold", (object?)line.Tax.GoldTaxBp ?? DBNull.Value);
            command.Parameters.AddWithValue("$taxWastage", (object?)line.Tax.WastageTaxBp ?? DBNull.Value);
            command.Parameters.AddWithValue("$taxMaking", (object?)line.Tax.MakingTaxBp ?? DBNull.Value);
            command.Parameters.AddWithValue("$taxStone", (object?)line.Tax.StoneTaxBp ?? DBNull.Value);
            command.Parameters.AddWithValue("$net", totals.NetWeightMg);
            command.Parameters.AddWithValue("$wastageMg", totals.WastageWeightMg);
            command.Parameters.AddWithValue("$metalValue", totals.MetalValuePaise);
            command.Parameters.AddWithValue("$wastageCharge", totals.WastageChargePaise);
            command.Parameters.AddWithValue("$makingCharges", totals.MakingChargesPaise);
            command.Parameters.AddWithValue("$stoneValue", totals.StoneValuePaise);
            command.Parameters.AddWithValue("$subtotal", totals.SubtotalPaise);
            command.Parameters.AddWithValue("$taxable", totals.TaxablePaise);
            command.Parameters.AddWithValue("$tax", totals.TaxPaise);
            command.Parameters.AddWithValue("$lineTotal", totals.LineTotalPaise);
            command.ExecuteNonQuery();
        }

        var stonePosition = 0;
        foreach (var stone in line.Stones)
        {
            stonePosition++;
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO line_stone (
                    id, order_line_id, position, category, name, origin_type, treatment, shape,
                    stone_count, total_carats_milli, colour_grade, clarity_grade,
                    certification_lab, certificate_number, stone_value_paise, setting_type)
                VALUES ($id, $lineId, $position, $category, $name, $origin, $treatment, $shape,
                        $count, $carats, $colour, $clarity, $lab, $certificate, $value, $setting);
                """;
            command.Parameters.AddWithValue("$id", stone.Id.ToString());
            command.Parameters.AddWithValue("$lineId", line.Id.ToString());
            command.Parameters.AddWithValue("$position", stonePosition);
            command.Parameters.AddWithValue("$category", stone.Category.ToString());
            command.Parameters.AddWithValue("$name", (object?)stone.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("$origin", (object?)stone.OriginType ?? DBNull.Value);
            command.Parameters.AddWithValue("$treatment", (object?)stone.Treatment ?? DBNull.Value);
            command.Parameters.AddWithValue("$shape", (object?)stone.Shape ?? DBNull.Value);
            command.Parameters.AddWithValue("$count", stone.StoneCount);
            command.Parameters.AddWithValue("$carats", stone.TotalCaratsMilli);
            command.Parameters.AddWithValue("$colour", (object?)stone.ColourGrade ?? DBNull.Value);
            command.Parameters.AddWithValue("$clarity", (object?)stone.ClarityGrade ?? DBNull.Value);
            command.Parameters.AddWithValue("$lab", (object?)stone.CertificationLab ?? DBNull.Value);
            command.Parameters.AddWithValue("$certificate", (object?)stone.CertificateNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("$value", stone.StoneValuePaise);
            command.Parameters.AddWithValue("$setting", (object?)stone.SettingType ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    public bool UpdateOrder(Order order)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sales_order SET
                status = $status, notes = $notes, cancellation_reason = $reason,
                expected_delivery_date = $expected, updated_at = $updated
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", order.Id.ToString());
        command.Parameters.AddWithValue("$status", order.Status.ToString());
        command.Parameters.AddWithValue("$notes", (object?)order.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$reason", (object?)order.CancellationReason ?? DBNull.Value);
        command.Parameters.AddWithValue("$expected", order.ExpectedDeliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        return command.ExecuteNonQuery() > 0;
    }

    public long NextOrderNumber() => NextNumber("order");
    public long NextDeliveryNumber() => NextNumber("delivery");
    public long NextPaymentNumber() => NextNumber("payment");

    private long NextNumber(string name)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            long next;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT next_value FROM number_sequence WHERE name = $name;";
                read.Parameters.AddWithValue("$name", name);
                var scalar = read.ExecuteScalar();
                if (scalar is null)
                {
                    throw new InvalidOperationException($"Sequence '{name}' is missing from number_sequence.");
                }

                next = Convert.ToInt64(scalar, CultureInfo.InvariantCulture) + 1;
            }

            using (var write = connection.CreateCommand())
            {
                write.Transaction = transaction;
                write.CommandText = "UPDATE number_sequence SET next_value = $next WHERE name = $name;";
                write.Parameters.AddWithValue("$next", next);
                write.Parameters.AddWithValue("$name", name);
                write.ExecuteNonQuery();
            }

            transaction.Commit();
            return next;
        }
    }

    // ------------------------------------------------------------------ deliveries

    public IReadOnlyList<Delivery> Deliveries
    {
        get
        {
            using var connection = Open();
            return LoadDeliveries(connection);
        }
    }

    private static List<Delivery> LoadDeliveries(SqliteConnection connection)
    {
        var deliveries = new List<Delivery>();
        var byId = new Dictionary<Guid, Delivery>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM delivery ORDER BY number DESC;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var delivery = new Delivery
                {
                    Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
                    Number = reader.GetInt64(reader.GetOrdinal("number")),
                    OrderId = Guid.Parse(reader.GetString(reader.GetOrdinal("order_id"))),
                    DeliveredOn = DateOnly.Parse(reader.GetString(reader.GetOrdinal("delivered_on")), CultureInfo.InvariantCulture),
                    ReceivedBy = reader.IsDBNull(reader.GetOrdinal("received_by")) ? null : reader.GetString(reader.GetOrdinal("received_by")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes"))
                };

                deliveries.Add(delivery);
                byId[delivery.Id] = delivery;
            }
        }

        if (deliveries.Count == 0)
        {
            return deliveries;
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM delivery_line;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var deliveryId = Guid.Parse(reader.GetString(reader.GetOrdinal("delivery_id")));
                if (byId.TryGetValue(deliveryId, out var delivery))
                {
                    delivery.Lines.Add(new DeliveryLine
                    {
                        OrderLineId = Guid.Parse(reader.GetString(reader.GetOrdinal("order_line_id"))),
                        Quantity = reader.GetInt32(reader.GetOrdinal("quantity"))
                    });
                }
            }
        }

        return deliveries;
    }

    public Delivery AddDelivery(Delivery delivery)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO delivery (id, number, order_id, delivered_on, received_by, notes, created_at)
                VALUES ($id, $number, $orderId, $deliveredOn, $receivedBy, $notes, $created);
                """;
            command.Parameters.AddWithValue("$id", delivery.Id.ToString());
            command.Parameters.AddWithValue("$number", delivery.Number);
            command.Parameters.AddWithValue("$orderId", delivery.OrderId.ToString());
            command.Parameters.AddWithValue("$deliveredOn", delivery.DeliveredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$receivedBy", (object?)delivery.ReceivedBy ?? DBNull.Value);
            command.Parameters.AddWithValue("$notes", (object?)delivery.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }

        foreach (var line in delivery.Lines)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO delivery_line (delivery_id, order_line_id, quantity)
                VALUES ($deliveryId, $lineId, $quantity);
                """;
            command.Parameters.AddWithValue("$deliveryId", delivery.Id.ToString());
            command.Parameters.AddWithValue("$lineId", line.OrderLineId.ToString());
            command.Parameters.AddWithValue("$quantity", line.Quantity);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        return delivery;
    }

    public int DeliveredPieces(Guid orderId, Guid? orderLineId = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = orderLineId is null
            ? """
              SELECT COALESCE(SUM(dl.quantity), 0)
              FROM delivery_line dl
              JOIN delivery d ON d.id = dl.delivery_id
              WHERE d.order_id = $orderId;
              """
            : """
              SELECT COALESCE(SUM(dl.quantity), 0)
              FROM delivery_line dl
              JOIN delivery d ON d.id = dl.delivery_id
              WHERE d.order_id = $orderId AND dl.order_line_id = $lineId;
              """;
        command.Parameters.AddWithValue("$orderId", orderId.ToString());
        if (orderLineId is not null)
        {
            command.Parameters.AddWithValue("$lineId", orderLineId.Value.ToString());
        }

        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    // ------------------------------------------------------------------ payments

    public IReadOnlyList<Payment> Payments
    {
        get
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM payment ORDER BY number DESC;";
            using var reader = command.ExecuteReader();

            var list = new List<Payment>();
            while (reader.Read())
            {
                list.Add(ReadPayment(reader));
            }

            return list;
        }
    }

    private static Payment ReadPayment(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
        Number = reader.GetInt64(reader.GetOrdinal("number")),
        OrderId = Guid.Parse(reader.GetString(reader.GetOrdinal("order_id"))),
        OrderLineId = reader.IsDBNull(reader.GetOrdinal("order_line_id"))
            ? null
            : Guid.Parse(reader.GetString(reader.GetOrdinal("order_line_id"))),
        AmountPaise = reader.GetInt64(reader.GetOrdinal("amount_paise")),
        PaidOn = DateOnly.Parse(reader.GetString(reader.GetOrdinal("paid_on")), CultureInfo.InvariantCulture),
        Method = reader.IsDBNull(reader.GetOrdinal("method")) ? null : reader.GetString(reader.GetOrdinal("method")),
        Reference = reader.IsDBNull(reader.GetOrdinal("reference")) ? null : reader.GetString(reader.GetOrdinal("reference")),
        IsRefund = reader.GetInt32(reader.GetOrdinal("is_refund")) == 1
    };

    public Payment AddPayment(Payment payment)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO payment (id, number, order_id, order_line_id, amount_paise, paid_on, method, reference, is_refund, created_at)
            VALUES ($id, $number, $orderId, $lineId, $amount, $paidOn, $method, $reference, $refund, $created);
            """;
        command.Parameters.AddWithValue("$id", payment.Id.ToString());
        command.Parameters.AddWithValue("$number", payment.Number);
        command.Parameters.AddWithValue("$orderId", payment.OrderId.ToString());
        command.Parameters.AddWithValue("$lineId", payment.OrderLineId is null ? DBNull.Value : payment.OrderLineId.Value.ToString());
        command.Parameters.AddWithValue("$amount", payment.AmountPaise);
        command.Parameters.AddWithValue("$paidOn", payment.PaidOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$method", (object?)payment.Method ?? DBNull.Value);
        command.Parameters.AddWithValue("$reference", (object?)payment.Reference ?? DBNull.Value);
        command.Parameters.AddWithValue("$refund", payment.IsRefund ? 1 : 0);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        return payment;
    }

    public long PaidPaise(Guid orderId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(CASE WHEN is_refund = 1 THEN -amount_paise ELSE amount_paise END), 0)
            FROM payment WHERE order_id = $orderId;
            """;
        command.Parameters.AddWithValue("$orderId", orderId.ToString());
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
