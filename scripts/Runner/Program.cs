var arguments = SendArguments.Parse(args);

if (arguments is null)
{
    Console.Error.WriteLine("Usage: send --local --variant <jit|r2r|aot|jit-chiseled>");
    return 2;
}

return await SendCommand.RunAsync(arguments);