switch (args)
{
    case ["send", ..]:
        var send = SendArguments.Parse(args);
        if (send is null)
        {
            PrintUsage();
            return 2;
        }

        return await SendCommand.RunAsync(send);

    case ["sample", ..]:
        var sample = SampleArguments.Parse(args, out var error);
        if (sample is null)
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 2;
        }

        return await SampleCommand.RunAsync(sample);

    case ["phases", ..]:
        var phases = PhasesArguments.Parse(args, out var phasesError);
        if (phases is null)
        {
            Console.Error.WriteLine(phasesError);
            PrintUsage();
            return 2;
        }

        return await PhasesCommand.RunAsync(phases);

    default:
        PrintUsage();
        return 2;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: send --local --variant <names>");
    Console.Error.WriteLine($"       {SampleArguments.Usage}");
    Console.Error.WriteLine($"       {PhasesArguments.Usage}");
    Console.Error.WriteLine("Names are comma-separated, from jit, r2r, aot and jit-chiseled.");
}