#!/bin/bash

# Script: script.sh
# Description: Generate Web API services
# Author: Robert Vejvoda + AI
# Date: $(date +%Y-%m-%d)

# Exit on error
set -e

# Variables
SOLUTION_NAME="FPS"

# Create new solution
dotnet new sln -n $SOLUTION_NAME

# Project: FPS.Core
PROJECT_NAME="FPS.Core"
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.HealthChecks
PROJECT_NAME="FPS.HealthChecks"
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Identity
PROJECT_NAME="FPS.Identity"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Audit
PROJECT_NAME="FPS.Audit"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Billing
PROJECT_NAME="FPS.Billing"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Booking
PROJECT_NAME="FPS.Booking"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Configuration
PROJECT_NAME="FPS.Configuration"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Customer
PROJECT_NAME="FPS.Customer"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Feedback
PROJECT_NAME="FPS.Feedback"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Notification 
PROJECT_NAME="FPS.Notification"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Profile
PROJECT_NAME="FPS.Profile"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj

# Project: FPS.Reporting
PROJECT_NAME="FPS.Reporting"
dotnet new webapi -n $PROJECT_NAME --use-controllers --no-https --output $PROJECT_NAME 
dotnet sln $SOLUTION_NAME.sln add $PROJECT_NAME/$PROJECT_NAME.csproj


# Exit successfully
exit 0