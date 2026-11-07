namespace Runner.Messaging;

internal static class ServiceBusSettings
{
    public const string DoneQueue = "done";

    // Read in Azure mode only. Connection strings never come from the command line, so they stay out of shell history.
    public const string ConnectionStringVariable = "SERVICEBUS_CONNECTION_STRING";

    // The emulator's fixed local connection string. SAS_KEY_VALUE is a literal placeholder, not a secret.
    public const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;"
        + "UseDevelopmentEmulator=true;";
}