using Microsoft.AspNetCore.Mvc;

namespace NewsApp2.Controllers
{
    public class BaseController : Controller
    {
        private readonly IWebHostEnvironment _host;
        public BaseController(IWebHostEnvironment host)
        {
            _host = host;
        }

        public string? UploadFile(string folder, IFormFile? file, string? fileUrl, string? isThereFile)
        {
            if (isThereFile == null) // في حال تم حذف الملف فقط
            {
                DeleteOldFile(fileUrl);
                return null;
            }

            if (file != null)// في حال تم تحميل ملف جديد
            {
                DeleteOldFile(fileUrl);

                string folderPath = Path.Combine(_host.WebRootPath, "upload", folder);
                if (!System.IO.File.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fileName = Guid.NewGuid() + "_" + Path.GetFileName(file.FileName);
                string newImageUrl = Path.Combine(folderPath, fileName);


                using (var stream = new FileStream(newImageUrl, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                return Path.Combine(folder, fileName).Replace("\\", "/"); // لحفظ المسار الجزئي في DB
            }
            return fileUrl; // في حال لم يتم تحميل ملف جديد يبقى الملف القديم كما هو
        }

        public void DeleteOldFile(string? fileUrl)
        {
            if (!string.IsNullOrEmpty(fileUrl))
            {
                // استبدال الفواصل المائلة للأمام للخلف في حال التشغيل على Windows
                string relativePath = fileUrl.Replace("/", Path.DirectorySeparatorChar.ToString());

                string fullPath = Path.Combine(_host.WebRootPath, "upload", relativePath);

                if (System.IO.File.Exists(fullPath))
                {
                    try
                    {
                        GC.Collect(); GC.WaitForPendingFinalizers(); // تجنّب مشكلة "الملف قيد الاستخدام"
                        System.IO.File.Delete(fullPath);
                    }
                    catch
                    {

                    }
                }
            }
        }

        public string ReadHtmlTemplate(string htmlTemplate)
        {
            var filePath = _host.WebRootPath
                            + Path.DirectorySeparatorChar + "templates"
                            + Path.DirectorySeparatorChar + htmlTemplate;
           
            StreamReader htmlFile = new StreamReader(filePath);
            string htmlString = htmlFile.ReadToEnd();
            htmlFile.Close();
            return htmlString;
        }

        public bool CheckImgExtension(IFormFile? img)
        {
            if (img != null)
            {
                string fileExtension = Path.GetExtension(img.FileName.ToLower());
                string[] validExtensions = { ".jpeg", ".jpg", ".bmp", ".gif", ".png", ".tiff", ".ico" };
                if (validExtensions.Contains(fileExtension))
                    return true;
                else
                    return false;
            }
            return true; /// في حال لا يوجد ملف
        }
    }


}
