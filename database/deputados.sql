CREATE DATABASE IF NOT EXISTS uc00614;
USE uc00614;

CREATE TABLE IF NOT EXISTS deputados (
    id INT PRIMARY KEY,
    nomeParlamentar VARCHAR(150),
    nomeCompleto VARCHAR(255),
    grupoParlamentar VARCHAR(20),
    circuloEleitoral VARCHAR(100),
    legislaturaId VARCHAR(20),
    situacao VARCHAR(50),
    updatedAt DATETIME
);
