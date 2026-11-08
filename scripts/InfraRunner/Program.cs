try
{
    switch (args)
    {
        case ["deploy", ..]:
            await DeployCommand.RunAsync(DeployArguments.Parse(args[1..]));
            return 0;
        case ["push-images", ..]:
            await PushImagesCommand.RunAsync(PushImagesArguments.Parse(args[1..]));
            return 0;
        case ["teardown", ..]:
            return await TeardownCommand.RunAsync(TeardownArguments.Parse(args[1..]));
        default:
            PrintUsage();
            return 2;
    }
}
catch (UsageException exception)
{
    Console.Error.WriteLine(exception.Message);
    PrintUsage();
    return 2;
}
catch (Exception exception) when (exception is InvalidOperationException or JsonException or Win32Exception)
{
    // A failing command has already printed its own error above this line.
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine(
        """
        Usage:
          deploy base --subscription-id <id> --budget-email <address>
                      [--name-prefix <prefix>] [--location <region>] [--preview] [--dry-run]
          deploy apps --subscription-id <id> --image-tag <tag>
                      [--min-replicas <n>] [--polling-interval <seconds>] [--cooldown-period <seconds>]
                      [--name-prefix <prefix>] [--location <region>] [--preview] [--dry-run]
          push-images --subscription-id <id> --image-tag <tag> [--variants <names>] [--forced-pulls <n>]
                      [--name-prefix <prefix>] [--dry-run]
          teardown    --subscription-id <id> [--name-prefix <prefix>] [--location <region>] [--dry-run]

        --preview runs what-if instead of the deployment, which changes nothing.
        --dry-run prints the az and docker commands and runs none of them.
        --forced-pulls also pushes n one-layer copies of each image, <variant>-<tag>-pull-01 and on, for the runner's
        --forced-pull.
        Defaults: --name-prefix stz, --location uksouth, --variants jit,r2r,aot,jit-chiseled,jit-identity.
        """);
}