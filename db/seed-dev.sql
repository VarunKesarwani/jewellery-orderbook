-- Jewellery Order Book — development seed
--
-- One customer and one order so the app has something to show on a fresh database.
-- Never run this against a real shop's database.

INSERT INTO customer (id, name, phone, address, pan, email, gstin, credit_days, notes, is_active, created_at, updated_at)
VALUES ('0199c0de-0000-7000-8000-000000000001', 'Ravi Jewellers', '9876543210',
        '12 Mount Road, Chennai 600002', 'ABCDE1234F', 'ravi@example.in', '33ABCDE1234F1Z5',
        15, 'Walk-in regular', 1, '2026-11-06T09:00:00Z', '2026-11-06T09:00:00Z');

INSERT INTO sales_order (id, number, customer_id, order_date, expected_delivery_date, status, notes, created_at, updated_at)
VALUES ('0199c0de-0000-7000-8000-000000000002', 1001,
        '0199c0de-0000-7000-8000-000000000001', '2026-11-06', '2026-11-20', 'Pending',
        'Rush order', '2026-11-06T09:05:00Z', '2026-11-06T09:05:00Z');

-- 22K ring, 8.500 g gross, 8% wastage, three diamonds, all four tax components at 3%.
-- The derived columns are the values the pricing engine produces for this input.
INSERT INTO order_line (
    id, order_id, position, item_name, item_description, quantity,
    metal_type, metal_purity, gross_weight_mg, rate_per_gram_paise, rate_date, wastage_pct_bp,
    making_type, making_rate, other_charges_paise, discount_paise,
    tax_gold_bp, tax_wastage_bp, tax_making_bp, tax_stone_bp,
    net_weight_mg, wastage_weight_mg, metal_value_paise, wastage_charge_paise,
    making_charges_paise, stone_value_paise, subtotal_paise, taxable_paise, tax_paise, line_total_paise)
VALUES (
    '0199c0de-0000-7000-8000-000000000003', '0199c0de-0000-7000-8000-000000000002', 1,
    '22K ring', 'Design 1042', 1,
    'Gold', '22K', 8500, 620000, '2026-11-06', 800,
    'PerGram', 45000, 50000, 100000,
    300, 300, 300, 300,
    8410, 673, 5214200, 417260, 378450, 1575000, 7634910, 7534910, 224548, 7759458);

INSERT INTO line_stone (
    id, order_line_id, position, category, name, origin_type, shape,
    stone_count, total_carats_milli, colour_grade, clarity_grade,
    certification_lab, certificate_number, stone_value_paise, setting_type)
VALUES (
    '0199c0de-0000-7000-8000-000000000004', '0199c0de-0000-7000-8000-000000000003', 1,
    'Diamond', 'Round brilliant', 'Natural', 'Round',
    3, 450, 'G', 'VVS1', 'GIA', '1234567890', 1575000, 'prong');
