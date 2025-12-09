namespace skt.IDE.Models;

public class PCodeRow
{
    public string Address { get; set; }
    public string Operation { get; set; }
    public string Operand { get; set; }
    public string Comment { get; set; }

    public PCodeRow(int address, string operation, int operand, string? comment = null)
    {
        Address = address.ToString();
        Operation = operation;
        Operand = operand.ToString();
        Comment = comment ?? "";
    }
}

