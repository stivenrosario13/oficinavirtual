internal record SupportDepartmentMetricRow(
	string Department,
	int Tickets,
	int Findings,
	int Solutions,
	int SupervisorTotal,
	int SupervisorPending,
	int SupervisorResolved);
