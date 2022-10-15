#!/bin/bash

# Wait 60 seconds for SQL Server to start up by ensuring that 
# calling SQLCMD does not return an error code, which will ensure that sqlcmd is accessible
# and that system and user databases return "0" which means all databases are in an "online" state
# https://docs.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-databases-transact-sql?view=sql-server-2017 

DBSTATUS=1
ERRCODE=1
i=0

while [[ $i -lt 90 ]] && [[ $ERRCODE -ne 0 ]] && [[ $DBSTATUS -ne 0 ]]; do
	i=$(($i+1))
	DBSTATUS=$(/opt/mssql-tools/bin/sqlcmd -h -1 -t 1 -U sa -P $SA_PASSWORD -Q "SET NOCOUNT ON; Select SUM(state) from sys.databases")
	ERRCODE=$?
	DBSTATUS=${DBSTATUS//[[:blank:]]/}
	[[ "$DBSTATUS" =~ ^[0-9]+$ ]] || DBSTATUS=255
	#echo ">>> STATUS: >$DBSTATUS< -- >$ERRCODE< -- >$i<"
	sleep 1s
done

echo "Finished waiting for database startup.."

if [[ $DBSTATUS -ne 0 ]] || [[ $ERRCODE -ne 0 ]]; then 
	echo "SQL Server took more than 90 seconds to start up or one or more databases are not in an ONLINE state"
	exit 1
fi

# Run the setup script to create the DB and the schema in the DB
/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P $SA_PASSWORD -d master -i /.env/initialize.sql

echo "SQL Server intialization completed.."

