# Future CRM Integration Tasks

## Small Tasks

1. Design SQLite schema for real estate CRM: real_state_agency, real_state_broker tables.
2. Implement foreign key relationships between tables.
3. Create migration scripts for SQLite database setup.
4. Integrate SQLite with existing .NET application.
5. Implement CRUD operations for properties and brokers.
6. Design data flow between DynamoDB (conversations) and SQLite (CRM data).
7. Implement data synchronization mechanisms.
8. Create API endpoints for CRM operations.
9. Test referential integrity in SQLite.
10. Plan migration strategy from current system to full CRM.
11. Document SQLite vs DynamoDB usage patterns.

---

## Unit Tests

1. Test SQLite schema creates all tables with correct columns and types.
2. Test foreign key constraints are enforced between tables.
3. Test CRUD operations for broker and property entities.
4. Test migration scripts run without errors on empty database.
5. Test migration scripts are idempotent (safe to run twice).

## Integration Tests

1. Test data sync: conversation fact in DynamoDB reflects in SQLite CRM record.
2. Test API endpoints return correct CRM data from SQLite.
3. Test referential integrity: deleting broker cascades correctly.
4. Test migration from current system preserves existing data.
5. Test concurrent reads and writes to SQLite under load.