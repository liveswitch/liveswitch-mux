namespace FM.LiveSwitch.Mux
{
    public enum VideoEventType
    {
        // the order is important here since
        // we sort same-timestamp events in
        // ascending order by type
        Add = 1,
        Update = 2,
        Remove = 3,
    }
}
