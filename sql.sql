USE EduVision
GO

CREATE TABLE [User] (
    userID INT IDENTITY(1,1) PRIMARY KEY,
    userName NVARCHAR(255),
    password NVARCHAR(255),
	is_verified BIT,
	fullName NVARCHAR(255),
    email NVARCHAR(255),
    created_at DATETIME,
    is_active BIT,
    role INT NOT NULL DEFAULT 0 -- 2: USER, 3: MANAGER, 1: ADMIN
);

CREATE TABLE OtpToken (
    id INT IDENTITY(1,1) PRIMARY KEY,
    email NVARCHAR(255),
    token NVARCHAR(255),
    created_at DATETIME DEFAULT GETDATE(),
    used BIT DEFAULT 0
);


CREATE TABLE Payment (
    paymentID INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT,
    amount DECIMAL(10,2),
    status NVARCHAR(50),
    created_at DATETIME,
    FOREIGN KEY (user_id) REFERENCES [User](userID)
);

CREATE TABLE Quota (
    quotaID INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT,
    daily_limit INT,
    used_week INT,
    last_reset DATE,
    status NVARCHAR(50),
    FOREIGN KEY (user_id) REFERENCES [User](userID)
);

CREATE TABLE Prompt (
    promptid INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT,
    content NVARCHAR(MAX),
    created_at DATETIME,
    status NVARCHAR(50),
    FOREIGN KEY (user_id) REFERENCES [User](userID)
);

CREATE TABLE Slide (
    slide_id INT IDENTITY(1,1) PRIMARY KEY,
    prompt_id INT,
    userID INT,
    type NVARCHAR(100),
    url NVARCHAR(MAX),
    status NVARCHAR(50),
    FOREIGN KEY (prompt_id) REFERENCES Prompt(promptid),
    FOREIGN KEY (userID) REFERENCES [User](userID)
);

CREATE TABLE Image (
    imageID INT IDENTITY(1,1) PRIMARY KEY,
    url NVARCHAR(MAX),
    status NVARCHAR(50),
    chapter NVARCHAR(100),
    category NVARCHAR(100),
    grade NVARCHAR(50),
);

CREATE TABLE GeneratedVideo (
    generateVideoID INT IDENTITY(1,1) PRIMARY KEY,
    prompt_id INT,
    slide_id INT,
    status NVARCHAR(50),
    duration_sec INT,
    resolution NVARCHAR(50),
    created_at DATETIME,
    video_url NVARCHAR(MAX),
    FOREIGN KEY (prompt_id) REFERENCES Prompt(promptid),
    FOREIGN KEY (slide_id) REFERENCES Slide(slide_id)
);


INSERT INTO [User] (userName, email, password, role, is_active, created_at)
VALUES 
    ('admin', 'admin@example.com', '12345', 1, 1, GETDATE()),
    ('manager', 'manager@example.com', '12345', 2, 1, GETDATE()),
    ('member', 'member@example.com', '12345', 3, 1, GETDATE());


INSERT INTO Image (url, status, chapter, category, grade)
VALUES 
('https://res.cloudinary.com/dtf2frp2w/image/upload/v1749101567/GDCD/Default/qmzvyja3ynr1nziqfpiy.png', 'active', 'None', 'GDCD', 'None'),
('https://res.cloudinary.com/dtf2frp2w/image/upload/v1749101700/GDCD/Default/r7ipe4zweud1i65rkz2g.png', 'active', 'None', 'GDCD', 'None'),
('https://res.cloudinary.com/dtf2frp2w/image/upload/v1749101735/GDCD/Default/vro16mtzerpgq618b6qe.jpg', 'active', 'None', 'GDCD', 'None'),
('https://res.cloudinary.com/dtf2frp2w/image/upload/v1749101821/GDCD/Default/mrr0xwm4pa4tlvrvr6fh.avif', 'active', 'None', 'GDCD', 'None'),
('https://res.cloudinary.com/dtf2frp2w/image/upload/v1749101821/GDCD/Default/mrr0xwm4pa4tlvrvr6fh.avif', 'active', 'None', 'GDCD', 'None');

