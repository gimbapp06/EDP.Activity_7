CREATE DATABASE IF NOT EXISTS thbms_demo;
USE thbms_demo;

CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL UNIQUE,
    password VARCHAR(255) NOT NULL,
    full_name VARCHAR(100) NOT NULL,
    role ENUM('admin','staff') DEFAULT 'staff',
    status ENUM('active','inactive') DEFAULT 'active',
    recovery_token VARCHAR(100) DEFAULT NULL,
    token_expiry BIGINT DEFAULT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS services (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    duration_minutes INT NOT NULL DEFAULT 60,
    price DECIMAL(10,2) NOT NULL,
    status ENUM('active','inactive') DEFAULT 'active'
);

CREATE TABLE IF NOT EXISTS therapists (
    id INT AUTO_INCREMENT PRIMARY KEY,
    full_name VARCHAR(100) NOT NULL,
    specialty VARCHAR(100),
    status ENUM('active','inactive') DEFAULT 'active'
);

CREATE TABLE IF NOT EXISTS appointments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    client_name VARCHAR(100) NOT NULL,
    client_email VARCHAR(100),
    client_phone VARCHAR(20),
    service_id INT NOT NULL,
    preferred_date DATE NOT NULL,
    preferred_time TIME NOT NULL,
    note TEXT,
    status ENUM('pending','confirmed','completed','cancelled') DEFAULT 'pending',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (service_id) REFERENCES services(id)
);

CREATE TABLE IF NOT EXISTS assignments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    appointment_id INT NOT NULL UNIQUE,
    therapist_id INT NOT NULL,
    assigned_by INT NOT NULL,
    assigned_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    notes TEXT,
    FOREIGN KEY (appointment_id) REFERENCES appointments(id),
    FOREIGN KEY (therapist_id)   REFERENCES therapists(id),
    FOREIGN KEY (assigned_by)    REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS payments (
    id INT AUTO_INCREMENT PRIMARY KEY,
    appointment_id INT NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    method ENUM('cash','gcash','credit_card','paymaya') DEFAULT 'cash',
    status ENUM('pending','completed','refunded','failed') DEFAULT 'pending',
    processed_by INT NOT NULL,
    processed_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    remarks TEXT,
    FOREIGN KEY (appointment_id) REFERENCES appointments(id),
    FOREIGN KEY (processed_by)   REFERENCES users(id)
);

-- ── SEED DATA

INSERT INTO users (username, email, password, full_name, role, status) VALUES
('admin',  'admin@thbms.com',  '$2y$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Admin User',  'admin', 'active'),
('staff1', 'staff1@thbms.com', '$2y$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Kim Apin',    'staff', 'active');