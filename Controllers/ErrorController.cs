using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace NewsApp2.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            var statusCodeResult = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
            switch (statusCode)
            {
                case 404:
                    ViewBag.Message = "The Resource you requested could not be found!!!";
                    ViewBag.Path = statusCodeResult?.OriginalPath;
                    break;
            }

            return View("NotFound");
        }

       
        [Route("Error")]
        public IActionResult Error()
        {
            var exceptionDetails = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            ViewBag.ExceptionPath = exceptionDetails?.Path;
            ViewBag.ExceptionMessage = exceptionDetails?.Error.Message;
            //ViewBag.ExceptionStackTrace = exceptionDetails?.Error.StackTrace; //تعطي نفس التريس الذي يظهر في صفحة المطور

            return View("Error");
        }
    }

}
