using System;

internal static class WeekendLoans
{
	public static bool Requester(DocumentActor a)
	{
		if (a.Role == "Technology" && a.Team == "TECHNICAL_FAILURE")
		{
			return AdministrativeProfiles.Login(a.Login) == "rosmarlin.vittini";
		}
		return false;
	}

	public static bool Valid(WeekendLoanPeriod p)
	{
		DayOfWeek dayOfWeek = p.From.DayOfWeek;
		bool flag = (uint)(dayOfWeek - 5) <= 1u;
		if (flag && p.To.DayOfWeek == DayOfWeek.Sunday)
		{
			int num = p.To.DayNumber - p.From.DayNumber;
			if (num >= 1 && num <= 2 && p.ReturnBy > p.To)
			{
				return p.ReturnBy.DayNumber - p.To.DayNumber <= 7;
			}
		}
		return false;
	}
}
