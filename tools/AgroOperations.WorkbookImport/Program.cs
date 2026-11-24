using AgroOperations.WorkbookImport;

return WorkbookImporter.Run(args, Environment.GetEnvironmentVariable("AGRO_LOCAL_DATABASE"), Console.Out, Console.Error);
