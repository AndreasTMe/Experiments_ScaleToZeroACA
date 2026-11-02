var arguments = SendArguments.Parse(args);

if (arguments is null)
{
    Console.Error.WriteLine("Usage: send --local --variant <names>");
    Console.Error.WriteLine("Names are comma-separated, from jit, r2r, aot and jit-chiseled.");
    return 2;
}

return await SendCommand.RunAsync(arguments);