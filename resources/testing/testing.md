Prerequisites: 
Required: PowerBI Desktop 
Optional: Tabular Editor (to )

Steps:


1. Open Sample.pbix in Power BI.

2. Go to External Tools -> Tabular Editor and notice your Port (in the window header)

3. Run InstanceDiscovery using "dotnet run --project InstanceDiscovery ;"

4. From CLI, select the PowerBI instance for testing (confirm with Port from step 2.)

5. Run test suite using "dotnet test --logger "console;verbosity=detailed";"


