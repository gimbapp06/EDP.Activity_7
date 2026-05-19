USE thbms_demo;

INSERT INTO therapists (full_name, specialty, status) VALUES
('Leon Thomas',    'Deep Tissue Massage',    'active'),
('Justin Bieber',  'Swedish Massage',        'active'),
('Taylor Swift',   'Aromatherapy Massage',   'active'),
('Lenny Kravitz',  'Hot Stone Massage',      'active'),
('Ariana Grande',  'Reflexology',            'active'),
('Post Malone',    'Head and Back Massage',  'active');

INSERT INTO services (name, description, duration_minutes, price, status) VALUES
('Swedish Massage',          'A gentle full-body massage using long strokes to improve circulation and relaxation.',         60,  600.00, 'active'),
('Hot Stone Massage',        'Smooth heated stones are placed on key points of the body to relieve muscle tension.',         90,  900.00, 'active'),
('Aromatherapy Massage',     'Uses essential oils blended to reduce stress, boost mood, and ease muscle pain.',              60,  750.00, 'active'),
('Deep Tissue Massage',      'Targets the deeper layers of muscle and connective tissue to release chronic tension.',        90,  950.00, 'active'),
('Reflexology',              'Applies pressure to specific points on the feet that correspond to organs and body systems.',  45,  450.00, 'active'),
('Head and Back Massage',    'Focused relief for the upper body — ideal for office workers with neck and shoulder pain.',    45,  500.00, 'active'),
('Shiatsu Massage',          'Japanese technique using finger pressure on meridian points to restore energy flow.',          60,  800.00, 'active'),
('Thai Massage',             'Combines acupressure, stretching, and yoga-like positions for full body flexibility.',         90, 1000.00, 'active'),
('Sports Massage',           'Designed for athletes — focuses on injury prevention and faster muscle recovery.',             60,  850.00, 'active'),
('Prenatal Massage',         'Specially adapted for pregnant women to reduce back pain, swelling, and stress.',              60,  700.00, 'active');