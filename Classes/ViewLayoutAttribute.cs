using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NewsApp2.Classes
{
    public class ViewLayoutAttribute : ResultFilterAttribute

    {
        private string _layout;
        public ViewLayoutAttribute(string layout)
        {
            _layout = layout;
        }

        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var viewResult = context.Result as ViewResult;

            if (viewResult != null)
            {
                viewResult.ViewData["Layout"] = _layout;
                //viewResult.ViewData["Layout"] = "_LayoutDashboar";
            }
        }



    }
}
