# E-Learning Platform API
A modular and scalable **E-Learning Platform API** built with **ASP.NET Core** following **Clean Architecture** principles.  
This project provides a complete backend solution for managing **users, courses, videos, exams, notifications, and authentication**.  
It implements **Repository Pattern**, **Unit of Work**, and **Result Pattern** to ensure clean separation of concerns, maintainability, and testability.

## Tech Stack
- **ASP.NET Core 10** – Web API framework  
- **Entity Framework Core** – ORM for database access  
- **SQLite** – Relational database  
- **Identity + JWT** – Authentication & Authorization  
- **Hangfire** – Background jobs & scheduling  
- **FluentValidation** – Request validation  
- **Explicit EF Core projections** – Object mapping (no AutoMapper)  
- **Twilio** – SMS notifications  
- **SMTP** – Email notifications  
- **OpenAPI + Scalar** – API documentation  

## Setup Instructions
1. **Clone the repository**  
   ```bash
   git clone https://github.com/Abdelrahman-Zagloul/E-Learning-Platform.git
2. Fill in your secrets in `ELearningPlatform.API/appsettings.Secrets.json`
   (git-ignored; template: `appsettings.Secrets.example.json`). It holds every password,
   key and token: JWT signing key, SMTP login, Twilio credentials, Google client id, admin
   and dev-user passwords. Nothing secret lives in `appsettings.json` or in code, and the app
   refuses to start while a required value is missing. Environment variables
   (e.g. `JWT__SecretKey`) override the file in production.

3. Run the API (migrations are applied and the admin account is seeded on startup):

   ```bash
   dotnet run --project ELearningPlatform.API
   ```

   API reference (Development): `https://localhost:7250/scalar/v1`

4. Run the tests:

   ```bash
   dotnet test --solution ELearningPlatform.slnx
   ```

