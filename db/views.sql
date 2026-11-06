-- Jewellery Order Book — views
--
-- Read-only projections used by the daily reports and by the order screens.
-- Keeping the aggregates here means the three reports and the app agree on how
-- a total is defined.

-- ---------------------------------------------------------------- order totals

DROP VIEW IF EXISTS v_order_totals;
CREATE VIEW v_order_totals AS
SELECT
    o.id                                        AS order_id,
    o.number                                    AS order_number,
    o.customer_id,
    c.name                                      AS customer_name,
    o.order_date,
    o.expected_delivery_date,
    o.status,
    COUNT(l.id)                                 AS line_count,
    COALESCE(SUM(l.quantity), 0)                AS total_pieces,
    COALESCE(SUM(l.subtotal_paise), 0)          AS subtotal_paise,
    COALESCE(SUM(l.tax_paise), 0)               AS tax_paise,
    COALESCE(SUM(l.line_total_paise), 0)        AS lines_total_paise,
    COALESCE(SUM(l.discount_paise), 0)          AS discount_paise,
    COALESCE(SUM(l.net_weight_mg), 0)           AS net_weight_mg,
    COALESCE(SUM(l.wastage_weight_mg), 0)       AS wastage_weight_mg,
    COALESCE((SELECT SUM(p.amount_paise) FROM payment p
              WHERE p.order_id = o.id AND p.is_refund = 0), 0)
      - COALESCE((SELECT SUM(p.amount_paise) FROM payment p
              WHERE p.order_id = o.id AND p.is_refund = 1), 0)
                                                AS paid_paise,
    COALESCE((SELECT SUM(dl.quantity) FROM delivery_line dl
              JOIN delivery d ON d.id = dl.delivery_id
              WHERE d.order_id = o.id), 0)      AS delivered_pieces
FROM sales_order o
JOIN customer c ON c.id = o.customer_id
LEFT JOIN order_line l ON l.order_id = o.id
GROUP BY o.id;

-- Final amount is the sum of line totals rounded to the nearest rupee; the
-- round-off is exposed separately so the bill and the total always agree.
DROP VIEW IF EXISTS v_order_summary;
CREATE VIEW v_order_summary AS
SELECT
    t.*,
    CAST(ROUND(t.lines_total_paise / 100.0) AS INTEGER) * 100          AS final_amount_paise,
    CAST(ROUND(t.lines_total_paise / 100.0) AS INTEGER) * 100
      - t.lines_total_paise                                           AS round_off_paise,
    CASE
        WHEN t.paid_paise <= 0 THEN 'Unpaid'
        WHEN t.paid_paise >= CAST(ROUND(t.lines_total_paise / 100.0) AS INTEGER) * 100 THEN 'Paid'
        ELSE 'PartiallyPaid'
    END                                                               AS payment_state,
    t.lines_total_paise - t.paid_paise                                AS balance_paise
FROM v_order_totals t;

-- ---------------------------------------------------------------- daily reports

DROP VIEW IF EXISTS v_daily_orders;
CREATE VIEW v_daily_orders AS
SELECT
    order_date                                  AS day,
    COUNT(*)                                    AS order_count,
    SUM(total_pieces)                           AS pieces,
    SUM(final_amount_paise)                     AS value_paise,
    SUM(tax_paise)                              AS tax_paise
FROM v_order_summary
WHERE status <> 'Cancelled'
GROUP BY order_date;

DROP VIEW IF EXISTS v_daily_deliveries;
CREATE VIEW v_daily_deliveries AS
SELECT
    d.delivered_on                              AS day,
    COUNT(DISTINCT d.id)                        AS delivery_count,
    COALESCE(SUM(dl.quantity), 0)               AS pieces
FROM delivery d
LEFT JOIN delivery_line dl ON dl.delivery_id = d.id
GROUP BY d.delivered_on;

DROP VIEW IF EXISTS v_daily_payments;
CREATE VIEW v_daily_payments AS
SELECT
    p.paid_on                                   AS day,
    COUNT(*)                                    AS payment_count,
    COALESCE(SUM(CASE WHEN p.is_refund = 0 THEN p.amount_paise END), 0) AS collected_paise,
    COALESCE(SUM(CASE WHEN p.is_refund = 1 THEN p.amount_paise END), 0) AS refunded_paise,
    COALESCE(SUM(CASE WHEN p.is_refund = 0 THEN p.amount_paise ELSE -p.amount_paise END), 0) AS net_paise
FROM payment p
GROUP BY p.paid_on;

-- ---------------------------------------------------------------- delivery progress

DROP VIEW IF EXISTS v_order_line_delivery;
CREATE VIEW v_order_line_delivery AS
SELECT
    l.id                                        AS order_line_id,
    l.order_id,
    l.position,
    l.item_name,
    l.quantity                                  AS ordered_pieces,
    COALESCE(SUM(dl.quantity), 0)               AS delivered_pieces,
    l.quantity - COALESCE(SUM(dl.quantity), 0)  AS remaining_pieces
FROM order_line l
LEFT JOIN delivery_line dl ON dl.order_line_id = l.id
GROUP BY l.id;
