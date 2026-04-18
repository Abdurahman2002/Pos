namespace NewsApp2.Classes.Helpers
{
    public class FileHelper
    {

        public static string? UploadFile(string folder, IFormFile? file, string? fileUrl, string? isThereFile, IWebHostEnvironment host)
        {
            if (isThereFile == null) // في حال تم حذف الملف فقط
            {
                DeleteOldFile(fileUrl, host);
                return null;
            }

            if (file != null)// في حال تم تحميل ملف جديد
            {
                DeleteOldFile(fileUrl, host);

                string folderPath = Path.Combine(host.WebRootPath, "Upload", folder);
                if (!File.Exists(folderPath))
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

        public static void DeleteOldFile(string? fileUrl, IWebHostEnvironment host)
        {
            if (!string.IsNullOrEmpty(fileUrl))
            {
                // استبدال الفواصل المائلة للأمام للخلف في حال التشغيل على Windows
                string relativePath = fileUrl.Replace("/", Path.DirectorySeparatorChar.ToString());

                string fullPath = Path.Combine(host.WebRootPath, "Upload", relativePath);

                if (File.Exists(fullPath))
                {
                    try
                    {
                        GC.Collect(); GC.WaitForPendingFinalizers(); // تجنّب مشكلة "الملف قيد الاستخدام"
                        File.Delete(fullPath);
                    }
                    catch
                    {

                    }
                }
            }
        }

        public static string ReadHtmlTemplate(string htmlTemplate, IWebHostEnvironment host)
        {
            var filePath = host.WebRootPath
                            + Path.DirectorySeparatorChar + "templates"
                            + Path.DirectorySeparatorChar + htmlTemplate;

            StreamReader htmlFile = new StreamReader(filePath);
            string htmlString = htmlFile.ReadToEnd();
            htmlFile.Close();
            return htmlString;
        }

        public static bool CheckImgExtension(IFormFile? img)
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
