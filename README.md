# Database Batch Executor

Database Batch Executor는 Mysql 쿼리를 배포하고 실행하는 프로그램입니다.

이 프로그램은 쿼리를 파싱하여 어떤 정보가 변경되었는지를 파악할 수 있습니다.

이를 통해 변경된 내용을 사용자에게 알려주고, 자동으로 배포 스크립트를 실행하여 변경된 내용을 데이터베이스에 반영할 수 있습니다.

데이터베이스의 스키마 변경 또는 데이터 변경 등의 작업을 식별합니다. 

변경된 내용을 사용자에게 알리거나, 자동 배포를 위한 스크립트를 생성합니다.

The Database Batch Executor is a program that deploys and executes MySQL queries.

It parses the queries to identify what information has been modified.

This allows it to notify users of the changes and automatically execute deployment scripts to reflect the modified content in the database.

It identifies operations such as schema modifications or data changes in the database.

It notifies users of the modified content and generates scripts for automatic deployment.


# config.json

```json
{
  "TablePath": "",
  "AlterTablePath": "",
  "StoredProcedurePath": "",
  "DbConfig": {
    "EndPoint": "",
    "Port": 3306,
    "UserId": "",
    "Password": "",
    "Database": ""
  },
  "Publish": {
    "PreDeployment": "",
    "PostDeployment": ""
  }
}
```

"TablePath": "CREATE TABLE" 문이 포함된 SQL 스크립트 파일의 경로 또는 위치를 지정합니다.

"AlterTablePath": "ALTER TABLE" 문이 포함된 SQL 스크립트 파일의 경로 또는 위치를 지정합니다.

"StoredProcedurePath": 저장 프로시저 정의가 포함된 SQL 스크립트 파일의 경로 또는 위치를 지정합니다.

"DbConfig": 데이터베이스 연결에 대한 구성 세부정보를 포함하며, 다음을 포함합니다:

"EndPoint": 데이터베이스 서버의 엔드포인트 또는 호스트 이름입니다.

"Port": 데이터베이스 서버에 연결하기 위한 포트 번호입니다 (기본값은 MySQL의 경우 3306입니다).

"UserId": 데이터베이스 연결에 사용되는 사용자 이름 또는 사용자 ID입니다.

"Password": 제공된 사용자 ID와 관련된 암호입니다.

"Database": 연결할 데이터베이스의 이름입니다.

"Publish": 배포 전 및 배포 후 실행할 선택적인 작업 또는 스크립트를 포함합니다:

"PreDeployment": SQL 변경 사항을 배포하기 전에 실행할 스크립트의 경로 또는 위치를 지정합니다.

"PostDeployment": SQL 변경 사항을 배포한 후에 실행할 스크립트의 경로 또는 위치를 지정합니다.

"TablePath": Specifies the path or location of the SQL script file containing the "CREATE TABLE" statements.

"AlterTablePath": Specifies the path or location of the SQL script file containing the "ALTER TABLE" statements.

"StoredProcedurePath": Specifies the path or location of the SQL script file containing the stored procedure definitions.

"DbConfig": Contains the configuration details for the database connection, including:

"EndPoint": The endpoint or hostname of the database server.

"Port": The port number to connect to the database server (default is 3306 for MySQL).

"UserId": The username or user ID for authenticating the database connection.

"Password": The password associated with the provided user ID.

"Database": The name of the database to connect to.

"Publish": Contains optional pre-deployment and post-deployment actions or scripts to be executed:

"PreDeployment": Specifies the path or location of the script to be executed before deploying the SQL changes.

"PostDeployment": Specifies the path or location of the script to be executed after deploying the SQL changes.