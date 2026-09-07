using Xunit;

// Each test class spins up its own real ASP.NET Core host via WebApplicationFactory<Program>,
// and Program.cs's Serilog bootstrap (Log.Logger = ...; later replaced by UseSerilog) mutates
// process-wide static state. Running multiple hosts concurrently in the same test process trips
// Serilog's "logger is already frozen" guard. Integration test classes are heavier than unit
// tests anyway — running them sequentially is the standard trade-off here.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
