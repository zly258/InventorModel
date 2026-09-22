namespace InventorModel.Addin;

internal sealed class AgentToolTrace
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public string Result { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public bool Succeeded { get; set; } = true;
}
