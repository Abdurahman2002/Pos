# Migration TODO (VS2022)

## New/Updated Fields
- Employee
  - Added `WarehouseId` (nullable GUID) to link employee to a single warehouse.
  - Added navigation `Warehouse`.
- Issue
  - Added `PatientName` (nullable string, max 200).

## DbContext / Model Changes
- No new DbSet required (Employee and Warehouse already exist).
- Ensure EF picks up `Employee.WarehouseId` FK to Warehouses.

## Roles/Claims
- Uses existing role `InventoryClerk` (already seeded earlier in project).
- No new roles/claims added in this step.

## Policy
- Added `ApprovedUserPolicy` (code-only). No migration needed.

## Migration Steps (VS2022)
1. Add a new migration (e.g., `AddEmployeeWarehouseId`).
2. Apply migration to database.
3. Verify `Employees` table has `WarehouseId` nullable FK.

## Notes
- Pending employees have `Approval = false`.
- Approval assigns warehouse and adds role `InventoryClerk`.
