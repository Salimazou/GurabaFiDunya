# Islam App - Quran Reminder iOS App with C# Backend

Een complete Koran herinnering iOS app met een C# ASP.NET Core backend. Deze app helpt gebruikers om hun dagelijkse Koran lezing bij te houden met herinneringen, audio streaming, streak tracking en een leaderboard systeem.

## 🚀 Features

### Frontend (iOS - Swift/SwiftUI)
- **Authenticatie**: Gebruikers registratie en login met JWT
- **Koran Audio**: Streaming van Koran audio met verschillende reciters
- **Herinneringen**: CRUD operaties voor dagelijkse lezing herinneringen
- **Klassement**: Leaderboard met streak visualisatie
- **Offline Support**: Core Data voor offline functionaliteit
- **Push Notificaties**: Lokale en remote notificaties
- **Nederlandse Lokalisatie**: Volledig vertaald naar het Nederlands

### Backend (C# ASP.NET Core)
- **RESTful API**: Volledige API voor alle app functionaliteiten
- **MongoDB**: NoSQL database voor flexibele data opslag
- **JWT Authenticatie**: Veilige gebruikers authenticatie
- **Audio Streaming**: Integratie met externe Koran audio APIs
- **Sync Endpoint**: Offline data synchronisatie
- **Swagger Documentatie**: Automatische API documentatie

## 📁 Project Structuur

```
IslamAPP/
├── IslamAppUI/                 # iOS SwiftUI App
│   ├── IslamAppUI/
│   │   ├── Models/            # Data modellen
│   │   ├── Views/             # SwiftUI views
│   │   ├── ViewModels/        # MVVM view models
│   │   ├── Services/          # API en Core Data services
│   │   └── Localization/      # Nederlandse vertalingen
│   └── IslamAppUI.xcodeproj/
├── ServerSideIslamApp/        # C# ASP.NET Core Backend
│   ├── IslamAppBackend/
│   │   ├── Controllers/       # API controllers
│   │   ├── Models/           # Data modellen
│   │   ├── Services/         # Business logic services
│   │   └── DTOs/             # Data transfer objects
│   └── Dockerfile
├── docker-compose.yml
└── README.md
```

## 🛠️ Setup & Installation

### Backend Setup

1. **Vereisten**:
   - .NET 8.0 SDK
   - MongoDB (lokaal of MongoDB Atlas)
   - Docker (optioneel)

2. **Lokale Development**:
   ```bash
   cd ServerSideIslamApp/IslamAppBackend
   dotnet restore
   dotnet run
   ```

3. **Met Docker**:
   ```bash
   docker-compose up -d
   ```

### iOS App Setup

1. **Vereisten**:
   - Xcode 15+
   - iOS 16.0+
   - macOS Ventura+

2. **Project Openen**:
   ```bash
   cd IslamAppUI
   open IslamAppUI.xcodeproj
   ```

3. **Dependencies**:
   - Alle dependencies zijn native Swift/SwiftUI
   - Geen externe package managers nodig

## 🐳 Docker Deployment

### Lokale Development
```bash
docker-compose up -d
```

### Productie Deployment (DigitalOcean)

1. **Server Setup**:
   ```bash
   # Update systeem
   sudo apt update && sudo apt upgrade -y
   
   # Installeer Docker
   curl -fsSL https://get.docker.com -o get-docker.sh
   sh get-docker.sh
   sudo usermod -aG docker $USER
   
   # Installeer Docker Compose
   sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
   sudo chmod +x /usr/local/bin/docker-compose
   ```

2. **App Deployment**:
   ```bash
   # Clone repository
   git clone <your-repo-url>
   cd IslamAPP
   
   # Stel environment variabelen in
   cp .env.example .env
   nano .env
   
   # Start applicatie
   docker-compose -f docker-compose.prod.yml up -d
   ```

3. **Environment Variabelen**:
   ```env
   MONGODB_CONNECTION_STRING=mongodb://admin:password@mongodb:27017/IslamApp?authSource=admin
   JWT_SECRET_KEY=your-super-secret-jwt-key-change-this-in-production
   JWT_ISSUER=IslamApp
   JWT_AUDIENCE=IslamAppUsers
   ```

## 🔧 Configuration

### Backend Configuration

**appsettings.json**:
```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "IslamApp"
  },
  "JWT": {
    "Key": "your-secret-key-here",
    "Issuer": "IslamApp",
    "Audience": "IslamAppUsers",
    "ExpirationDays": 7
  }
}
```

### iOS Configuration

Update de API base URL in `APIService.swift`:
```swift
private let baseURL = "https://your-domain.com/api"
```

## 📱 API Endpoints

### Authenticatie
- `POST /api/auth/register` - Gebruiker registratie
- `POST /api/auth/login` - Gebruiker login
- `GET /api/auth/profile` - Gebruiker profiel

### Herinneringen
- `GET /api/reminders` - Lijst van herinneringen
- `POST /api/reminders` - Nieuwe herinnering
- `PUT /api/reminders/{id}` - Update herinnering
- `DELETE /api/reminders/{id}` - Verwijder herinnering
- `POST /api/reminders/{id}/mark` - Markeer als voltooid

### Koran
- `GET /api/quran/reciters` - Lijst van reciters
- `GET /api/quran/surahs` - Lijst van surahs
- `GET /api/quran/audio` - Audio URL

### Klassement
- `GET /api/leaderboard` - Leaderboard data

### Synchronisatie
- `POST /api/sync` - Offline data sync

## 🔒 Security

### Backend Security
- JWT token authenticatie
- HTTPS enforced in productie
- CORS configuratie
- Input validatie
- MongoDB connection security

### iOS Security
- Keychain storage voor tokens
- Certificate pinning (aanbevolen voor productie)
- App Transport Security (ATS)

## 🚀 Deployment op DigitalOcean

### 1. Droplet Setup
```bash
# Maak een nieuwe droplet (Ubuntu 22.04)
# Minimaal 2GB RAM, 1 vCPU

# SSH naar je droplet
ssh root@your-droplet-ip
```

### 2. Docker Installation
```bash
# Update packages
apt update && apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Install Docker Compose
curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose
```

### 3. App Deployment
```bash
# Clone your repository
git clone https://github.com/your-username/IslamAPP.git
cd IslamAPP

# Set environment variables
export MONGODB_CONNECTION_STRING="mongodb://admin:islamapp2024@mongodb:27017/IslamApp?authSource=admin"
export JWT_SECRET_KEY="your-super-secret-jwt-key-change-this-in-production-2024"

# Start the application
docker-compose up -d

# Check status
docker-compose ps
```

### 4. Domain & SSL Setup
```bash
# Install Nginx
apt install nginx -y

# Configure reverse proxy
nano /etc/nginx/sites-available/islamapp

# Add SSL with Let's Encrypt
apt install certbot python3-certbot-nginx -y
certbot --nginx -d your-domain.com
```

### 5. Monitoring
```bash
# Check logs
docker-compose logs -f backend

# Monitor resources
docker stats
```

## 📊 Database Schema

### Users Collection
```json
{
  "_id": "ObjectId",
  "name": "string",
  "email": "string",
  "passwordHash": "string",
  "streakCount": "number",
  "lastActiveDate": "date",
  "createdAt": "date",
  "updatedAt": "date"
}
```

### Reminders Collection
```json
{
  "_id": "ObjectId",
  "userId": "ObjectId",
  "title": "string",
  "startTime": "string",
  "endTime": "string",
  "frequency": "string",
  "isActive": "boolean",
  "createdAt": "date",
  "updatedAt": "date"
}
```

## 🤝 Contributing

1. Fork het project
2. Maak een feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit je changes (`git commit -m 'Add some AmazingFeature'`)
4. Push naar de branch (`git push origin feature/AmazingFeature`)
5. Open een Pull Request

## 📄 License

Dit project is gelicenseerd onder de MIT License - zie het [LICENSE](LICENSE) bestand voor details.

## 📞 Contact

Voor vragen of support, neem contact op via:
- Email: support@islamapp.nl
- GitHub Issues: [Create an issue](https://github.com/your-username/IslamAPP/issues)

## 🙏 Acknowledgments

- Quran audio van [QuranicAudio.com](https://quranicaudio.com)
- MongoDB voor de database
- ASP.NET Core voor de backend framework
- SwiftUI voor de iOS interface 