using Microsoft.AspNetCore.Http;
using Phantasma.Infrastructure.API.Interfaces;

namespace Phantasma.Infrastructure.Utilities;

public class ServiceUtility
{
    // return HttpContext.Items["APIService"] as IAPIService;
    public static IAPIService GetAPIService(HttpContext httpContext)
    {
        return httpContext.Items["APIService"] as IAPIService;
    }
    

}
