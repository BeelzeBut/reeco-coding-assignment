namespace OrderOps.Api.Data.Entities;

public sealed class Job
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public string Action { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
