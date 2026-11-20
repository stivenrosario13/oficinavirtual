static class WarehouseRequestSource
{
    // Technology tickets belong to TECHNOLOGY even when the requester manages
    // one of its teams. Document management scope must not change ticket origin.
    public static DocumentActor? Resolve(DocumentActor actor,string requested)
    {
        if(requested=="")return actor;
        if(!DepartmentAccounts.Names.ContainsValue(requested))return null;
        if(actor.Manager||requested==actor.Department)return actor with{AssignedDepartment=requested};
        if(actor.Role=="Technology"&&actor.Team!="WAREHOUSE")
        {
            var teamDepartment=actor.Team switch{"WORKSHOP"=>"Taller","CALL_CENTER"=>"Call Center","TECHNICAL_FAILURE"=>"Avería Técnica",_=>null};
            if(requested=="Tecnología"||requested==teamDepartment)return actor with{AssignedDepartment=requested};
        }
        return null;
    }

    public static WarehouseRequestInfo? RouteFailure(WarehouseRequestInfo request,DocumentActor actor,string sourceDepartment)
    {
        if(request.FailureCategory is null)return request;
        // Every requester may route a replacement to either maintenance department.
        // The requesting department remains on the document; it is not the resolver.
        var department=request.FailureDepartment;
        if(department is not ("TECHNOLOGY" or "GENERAL_SERVICES"))return null;
        var team=department=="TECHNOLOGY"?actor.Team switch{"CALL_CENTER"=>"CALL_CENTER","TECHNICAL_FAILURE"=>"TECHNICAL_FAILURE",_=>"TECHNICAL_FAILURE"}:null;
        return request with{FailureTeam=team};
    }
}
