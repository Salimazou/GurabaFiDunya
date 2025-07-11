#!/bin/bash

# Islam App Deployment Script voor DigitalOcean
# Gebruik: ./deploy.sh [production|development]

set -e

MODE=${1:-development}

echo "🚀 Starting Islam App deployment in $MODE mode..."

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Installing Docker..."
    curl -fsSL https://get.docker.com -o get-docker.sh
    sh get-docker.sh
    sudo usermod -aG docker $USER
    echo "✅ Docker installed successfully!"
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose is not installed. Installing Docker Compose..."
    sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    sudo chmod +x /usr/local/bin/docker-compose
    echo "✅ Docker Compose installed successfully!"
fi

# Create necessary directories
mkdir -p mongodb-init
mkdir -p logs

# Set environment variables based on mode
if [ "$MODE" = "production" ]; then
    echo "🔧 Setting up production environment..."
    
    # Prompt for production secrets if not set
    if [ -z "$JWT_SECRET_KEY" ]; then
        echo "Please enter a strong JWT secret key:"
        read -s JWT_SECRET_KEY
        export JWT_SECRET_KEY
    fi
    
    if [ -z "$MONGODB_PASSWORD" ]; then
        echo "Please enter MongoDB password:"
        read -s MONGODB_PASSWORD
        export MONGODB_PASSWORD
    fi
    
    # Use production compose file
    COMPOSE_FILE="docker-compose.prod.yml"
    
    # Create production compose file if it doesn't exist
    if [ ! -f "$COMPOSE_FILE" ]; then
        cp docker-compose.yml docker-compose.prod.yml
        # Update ports for production (remove external MongoDB port)
        sed -i 's/- "27017:27017"/#- "27017:27017"/' docker-compose.prod.yml
        sed -i 's/- "5000:80"/- "80:80"/' docker-compose.prod.yml
    fi
else
    echo "🔧 Setting up development environment..."
    COMPOSE_FILE="docker-compose.yml"
    
    # Set default development secrets
    export JWT_SECRET_KEY="development-secret-key-change-in-production"
    export MONGODB_PASSWORD="islamapp2024"
fi

# Stop existing containers
echo "🛑 Stopping existing containers..."
docker-compose -f $COMPOSE_FILE down

# Pull latest images
echo "📦 Pulling latest images..."
docker-compose -f $COMPOSE_FILE pull

# Build and start containers
echo "🏗️ Building and starting containers..."
docker-compose -f $COMPOSE_FILE up -d --build

# Wait for services to be ready
echo "⏳ Waiting for services to start..."
sleep 30

# Check if services are running
echo "🔍 Checking service health..."
if docker-compose -f $COMPOSE_FILE ps | grep -q "Up"; then
    echo "✅ Services are running!"
    
    # Display service URLs
    echo ""
    echo "🌐 Service URLs:"
    if [ "$MODE" = "production" ]; then
        echo "   Backend API: http://your-domain.com"
        echo "   API Documentation: http://your-domain.com/swagger"
    else
        echo "   Backend API: http://localhost:5000"
        echo "   API Documentation: http://localhost:5000/swagger"
        echo "   MongoDB: mongodb://localhost:27017"
    fi
    
    echo ""
    echo "📱 iOS App Configuration:"
    echo "   Update APIService.swift with your backend URL"
    echo ""
    echo "🔧 Useful commands:"
    echo "   View logs: docker-compose -f $COMPOSE_FILE logs -f"
    echo "   Stop services: docker-compose -f $COMPOSE_FILE down"
    echo "   Restart: docker-compose -f $COMPOSE_FILE restart"
    
else
    echo "❌ Some services failed to start. Check logs:"
    docker-compose -f $COMPOSE_FILE logs
    exit 1
fi

echo ""
echo "🎉 Islam App deployment completed successfully!"

if [ "$MODE" = "production" ]; then
    echo ""
    echo "🔒 Production Security Checklist:"
    echo "   ✓ Change default MongoDB password"
    echo "   ✓ Use strong JWT secret key"
    echo "   ✓ Configure SSL/HTTPS"
    echo "   ✓ Set up firewall rules"
    echo "   ✓ Configure backup strategy"
    echo "   ✓ Set up monitoring"
fi 