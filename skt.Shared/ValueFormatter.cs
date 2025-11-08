namespace skt.Shared;

/// <summary>
/// Utility class for formatting values consistently across the application
/// </summary>
public static class ValueFormatter
{
  /// <summary>
  /// Formats a value as a string, ensuring floats display with decimal point
  /// </summary>
  public static string FormatValue(object? value)
  {
    if (value == null)
      return "";

    return value switch
    {
      float floatValue => floatValue.ToString("0.0###############"),
      double doubleValue => doubleValue.ToString("0.0###############"),
      _ => value.ToString() ?? ""
    };
  }
}
