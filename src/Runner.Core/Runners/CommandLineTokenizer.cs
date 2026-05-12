namespace Runner.Core.Runners;

public static class CommandLineTokenizer
{
    public static IReadOnlyList<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var args = new List<string>();
        var current = new List<char>();
        var inQuotes = false;
        var escaping = false;

        foreach (var character in value)
        {
            if (escaping)
            {
                current.Add(character);
                escaping = false;
                continue;
            }

            if (character == '\\')
            {
                escaping = true;
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrent(args, current);
                continue;
            }

            current.Add(character);
        }

        if (escaping)
        {
            current.Add('\\');
        }

        AddCurrent(args, current);
        return args;
    }

    private static void AddCurrent(List<string> args, List<char> current)
    {
        if (current.Count == 0)
        {
            return;
        }

        args.Add(new string(current.ToArray()));
        current.Clear();
    }
}
