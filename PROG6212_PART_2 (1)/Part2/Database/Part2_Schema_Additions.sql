-- Part 2 additions to the Part 1 schema.
-- The Part 2 brief says every event must capture a distance and an event type (run, walk, cycle).
-- Part 1's Events table did not have these two columns, so Entity Framework adds them.
-- You only need to run this script if you created the database from the Part 1 SQL scripts
-- instead of using the EF Core migration.

ALTER TABLE Events ADD EventType VARCHAR(10) NOT NULL DEFAULT 'Run';
ALTER TABLE Events ADD DistanceKm DECIMAL(6,2) NOT NULL DEFAULT 0;
