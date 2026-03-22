namespace Ed;

public enum EdPrintMode
{
    Normal,
    Numbered,
    Literal,
}

public enum EdWriteMode
{
    Replace,
    Append,
}

public enum EdSearchDirection
{
    Forward,
    Backward,
}

public enum EdGlobalMode
{
    Match,
    NonMatch,
    InteractiveMatch,
    InteractiveNonMatch,
}
