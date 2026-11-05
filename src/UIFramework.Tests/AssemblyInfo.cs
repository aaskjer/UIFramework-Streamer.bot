using Xunit;

// One window shows one language: Text.Use and Entry.Prepare set a process-wide language. Test classes running
// side by side would reset it under each other's feet (the CI run of 2026-11-05 saw "Save" where "Speichern" was
// due), so the classes run one after the other. The whole suite takes a few seconds either way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
