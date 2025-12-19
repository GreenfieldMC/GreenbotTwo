using System.Text.Json.Serialization;

namespace GreenbotTwo.Models;

public record BuildCode(long BuildCodeId, int ListOrder, string Code);