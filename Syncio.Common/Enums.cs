namespace Syncio.Common
{
	public enum ConflictPolicy
	{
		HubWin = 1,
		MemberWin = 2
	}

	public enum Operation
	{
		Update = 1,
		Insert = 2,
		Delete = 3
	}

	public enum LogLevel
	{
		Verbose = 1,
		Info = 2,
		Important = 3,
		Exception = 4
	}

	public enum HistoryStrategyType
	{
		None = 0,
		ProcessAndDelete = 1,
		PeriodicDelete = 2
	}

	public enum SyncRole
	{
		None = 0,
		Hub = 1,
		Member = 2
	}

	public enum RetryPolicy
    {
		None = 0,
		RetryOnce = 1
    }

    public enum LogPolicy
    {
        None = 0,
        Everything = 1,
        NotSuccessful = 2,
        OnException = 3,
    }
}
