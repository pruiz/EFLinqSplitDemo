#!/bin/bash

# Start the script to create the DB and user
/.env/initialize.sh &

# Start SQL Server
/opt/mssql/bin/sqlservr

