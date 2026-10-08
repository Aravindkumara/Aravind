-- =====================================================================
-- OEM Product Service Excellence - sample/seed data
-- All seeded users have the password:  Pass@123  (change after first login)
-- =====================================================================

INSERT INTO dealers (id, code, name, city, state, contact_phone, email) VALUES
    (1, 'DLR-BLR-001', 'Prime Motors Bengaluru', 'Bengaluru', 'Karnataka',  '+91-80-40001234', 'service@primemotors.example'),
    (2, 'DLR-CHN-002', 'Coastal Auto Chennai',   'Chennai',   'Tamil Nadu', '+91-44-40005678', 'workshop@coastalauto.example')
ON CONFLICT DO NOTHING;

INSERT INTO users (id, username, password_hash, full_name, email, role, dealer_id) VALUES
    (1, 'admin',    '$2b$11$W8VqBWbCNWWIc/Yi0CrRqe9hTm661N5IPOyE1WcT6Iy3dGCOPd6lK', 'System Administrator', 'admin@oem.example',    'Admin',    NULL),
    (2, 'author',   '$2b$11$W8VqBWbCNWWIc/Yi0CrRqe9hTm661N5IPOyE1WcT6Iy3dGCOPd6lK', 'Service Engineer',     'author@oem.example',   'Author',   NULL),
    (3, 'approver', '$2b$11$W8VqBWbCNWWIc/Yi0CrRqe9hTm661N5IPOyE1WcT6Iy3dGCOPd6lK', 'Service Quality Head', 'approver@oem.example', 'Approver', NULL),
    (4, 'dealer',   '$2b$11$W8VqBWbCNWWIc/Yi0CrRqe9hTm661N5IPOyE1WcT6Iy3dGCOPd6lK', 'Workshop Technician',  'tech@primemotors.example', 'Dealer', 1)
ON CONFLICT DO NOTHING;

INSERT INTO divisions (id, code, name, description) VALUES
    (1, 'PV', 'Passenger Vehicles',  'Cars and utility vehicles'),
    (2, 'CV', 'Commercial Vehicles', 'Trucks and buses'),
    (3, 'TW', 'Two Wheelers',        'Motorcycles and scooters')
ON CONFLICT DO NOTHING;

INSERT INTO model_categories (id, division_id, code, name, description) VALUES
    (1, 1, 'PV-SUV',   'SUV',             'Sport utility vehicles'),
    (2, 1, 'PV-SEDAN', 'Sedan',           'Three-box passenger cars'),
    (3, 2, 'CV-LCV',   'Light Commercial','Pickups and small trucks'),
    (4, 3, 'TW-MC',    'Motorcycle',      'Motorcycles')
ON CONFLICT DO NOTHING;

INSERT INTO models (id, category_id, code, name, description) VALUES
    (1, 1, 'SUV-TERRA', 'Terra',   'Mid-size SUV'),
    (2, 2, 'SED-AURA',  'Aura',    'Compact sedan'),
    (3, 3, 'LCV-HAUL',  'Haul 1T', '1 tonne pickup'),
    (4, 4, 'MC-BOLT',   'Bolt 150','150cc commuter motorcycle')
ON CONFLICT DO NOTHING;

INSERT INTO variants (id, model_id, code, name, description) VALUES
    (1, 1, 'TERRA-D-MT', 'Terra Diesel MT', '2.2L diesel, 6-speed manual'),
    (2, 1, 'TERRA-P-AT', 'Terra Petrol AT', '1.5L turbo petrol, 6-speed automatic'),
    (3, 2, 'AURA-P-MT',  'Aura Petrol MT',  '1.2L petrol, 5-speed manual'),
    (4, 3, 'HAUL-D-MT',  'Haul Diesel MT',  '2.5L diesel, 5-speed manual'),
    (5, 4, 'BOLT-STD',   'Bolt Standard',   'Drum brake, kick + self start')
ON CONFLICT DO NOTHING;

INSERT INTO assemblies (id, variant_id, parent_id, level, code, name, description) VALUES
    (1, 1, NULL, 1, 'ENG',          'Engine',              '2.2L mHawk diesel engine'),
    (2, 1, 1,    2, 'ENG-FUEL',     'Fuel System',         'Common rail fuel injection'),
    (3, 1, 2,    3, 'ENG-FUEL-INJ', 'Fuel Injector',       'Piezo injector'),
    (4, 1, 1,    2, 'ENG-LUBE',     'Lubrication System',  'Oil pump, filter, sump'),
    (5, 1, NULL, 1, 'BRK',          'Brakes',              'Hydraulic disc/drum brakes'),
    (6, 1, 5,    2, 'BRK-FRONT',    'Front Disc Brake',    'Ventilated disc with caliper'),
    (7, 2, NULL, 1, 'ENG',          'Engine',              '1.5L turbo petrol engine'),
    (8, 2, NULL, 1, 'TRN',          'Transmission',        '6-speed torque converter AT')
ON CONFLICT DO NOTHING;

-- Sample SOPs ---------------------------------------------------------
INSERT INTO sops (id, sop_code, version, title, activity_type, purpose, division_id, category_id, model_id, variant_id, assembly_id,
                  standard_time_minutes, skill_level, safety_notes, status, created_by, created_at, submitted_at, approved_by, approved_at, published_at) VALUES
    (1, 'SOP-PV-PDI-001', 1, 'Pre-Delivery Inspection - Passenger Vehicles', 'PDI',
        'Standard checks before handing over any new passenger vehicle to the customer.',
        1, NULL, NULL, NULL, NULL, 90, 'Technician L1',
        'Wear safety shoes. Use wheel chocks. Do not start the engine in a closed bay without exhaust extraction.',
        'Published', 2, now() - interval '20 days', now() - interval '19 days', 3, now() - interval '18 days', now() - interval '18 days'),
    (2, 'SOP-TERRA-FSV-001', 1, 'Terra Diesel - Engine Oil & Filter Replacement', 'PeriodicMaintenance',
        'Replace engine oil and oil filter at scheduled service intervals (every 10,000 km / 12 months).',
        1, 1, 1, 1, 4, 45, 'Technician L1',
        'Hot oil can cause burns - allow the engine to cool for 15 minutes. Dispose used oil as per environmental norms.',
        'Published', 2, now() - interval '10 days', now() - interval '9 days', 3, now() - interval '8 days', now() - interval '8 days'),
    (3, 'SOP-TERRA-INJ-001', 1, 'Terra Diesel - Fuel Injector Replacement', 'Repair',
        'Removal and installation of a common-rail fuel injector.',
        1, 1, 1, 1, 3, 120, 'Technician L3',
        'Fuel system is under very high pressure. Wait at least 5 minutes after switching off before opening any fuel line.',
        'Draft', 2, now() - interval '2 days', NULL, NULL, NULL, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO sop_steps (sop_id, step_no, title, instruction, tools_required, specification, caution, estimated_minutes) VALUES
    (1, 1, 'Exterior inspection',      'Walk around the vehicle and check paint, glass, lamps and panel gaps for transit damage.', NULL, NULL, 'Record any damage with photographs before proceeding.', 15),
    (1, 2, 'Fluid levels',             'Check engine oil, coolant, brake fluid, washer fluid and power steering fluid levels.', 'Torch', 'Between MIN and MAX marks', NULL, 15),
    (1, 3, 'Tyre pressure',            'Check and set tyre pressure on all wheels including spare.', 'Tyre pressure gauge', 'As per door placard', NULL, 10),
    (1, 4, 'Electrical & road test',   'Check all lamps, horn, wipers, infotainment, then conduct a 5 km road test.', 'Diagnostic scanner', 'No DTCs stored', 'Road test only with trade plate and authorised driver.', 50),
    (2, 1, 'Lift and drain',           'Raise the vehicle on the lift, place drain tray and remove drain plug. Allow oil to drain completely.', '17 mm socket, drain tray', NULL, 'Oil may be hot.', 15),
    (2, 2, 'Replace oil filter',       'Remove the oil filter, clean the mounting face, lubricate the new seal and fit the new filter.', 'Oil filter wrench', 'Hand tight + 3/4 turn', NULL, 10),
    (2, 3, 'Refit drain plug and fill','Fit a new drain plug washer, torque the plug, lower the vehicle and fill fresh oil.', 'Torque wrench, funnel', 'Drain plug 35 Nm; oil 6.5 L 5W-30', NULL, 15),
    (2, 4, 'Verify and reset',         'Run engine for 2 minutes, check for leaks, recheck level and reset the service reminder.', 'Diagnostic scanner', NULL, NULL, 5),
    (3, 1, 'Depressurise fuel rail',   'Switch off ignition, wait 5 minutes and disconnect battery negative terminal.', '10 mm spanner', NULL, 'High pressure fuel - follow wait time.', 10);

INSERT INTO sop_resources (sop_id, resource_type, part_number, description, quantity, uom) VALUES
    (2, 'Part',       '0305AAA00170N', 'Oil filter element',          1,   'Nos'),
    (2, 'Part',       '0305AAA01111N', 'Drain plug washer',           1,   'Nos'),
    (2, 'Consumable', 'OIL-5W30-SYN',  'Engine oil 5W-30 synthetic',  6.5, 'Ltr'),
    (2, 'Tool',       NULL,            'Torque wrench 20-100 Nm',     1,   'Nos'),
    (3, 'Tool',       'ST-INJ-PULLER', 'Injector puller special tool',1,   'Nos');

INSERT INTO audit_logs (entity_type, entity_id, action, details, user_id, username, created_at) VALUES
    ('Sop', 1, 'Created',   'SOP-PV-PDI-001 v1 created',        2, 'author',   now() - interval '20 days'),
    ('Sop', 1, 'Published', 'SOP-PV-PDI-001 v1 published',      3, 'approver', now() - interval '18 days'),
    ('Sop', 2, 'Created',   'SOP-TERRA-FSV-001 v1 created',     2, 'author',   now() - interval '10 days'),
    ('Sop', 2, 'Published', 'SOP-TERRA-FSV-001 v1 published',   3, 'approver', now() - interval '8 days'),
    ('Sop', 3, 'Created',   'SOP-TERRA-INJ-001 v1 created',     2, 'author',   now() - interval '2 days');

-- Move sequences past the explicit ids used above
SELECT setval('dealers_id_seq',          (SELECT COALESCE(MAX(id), 1) FROM dealers));
SELECT setval('users_id_seq',            (SELECT COALESCE(MAX(id), 1) FROM users));
SELECT setval('divisions_id_seq',        (SELECT COALESCE(MAX(id), 1) FROM divisions));
SELECT setval('model_categories_id_seq', (SELECT COALESCE(MAX(id), 1) FROM model_categories));
SELECT setval('models_id_seq',           (SELECT COALESCE(MAX(id), 1) FROM models));
SELECT setval('variants_id_seq',         (SELECT COALESCE(MAX(id), 1) FROM variants));
SELECT setval('assemblies_id_seq',       (SELECT COALESCE(MAX(id), 1) FROM assemblies));
SELECT setval('sops_id_seq',             (SELECT COALESCE(MAX(id), 1) FROM sops));
