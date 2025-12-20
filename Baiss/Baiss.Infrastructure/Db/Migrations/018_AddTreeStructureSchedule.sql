-- Add TreeStructureSchedule and TreeStructureScheduleEnabled columns to Settings table

ALTER TABLE Settings ADD COLUMN TreeStructureSchedule TEXT DEFAULT '0 0 0 * * ?';
ALTER TABLE Settings ADD COLUMN TreeStructureScheduleEnabled INTEGER DEFAULT 0;
