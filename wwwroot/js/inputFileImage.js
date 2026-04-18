function clickFile(input) {
    var objInput = document.getElementById(input);
    objInput.click();
    return false;
}
function UploadImageFile(input, img, isThereImg, defaultText) {
    var file = input.files[0];
    if (/\.(jpe?g|png|bmp|tiff|gif|ico)$/i.test(file.name.toLowerCase()) === false) {
        alert("الملف المرفق ليس ملف صورة");
    }
    else {
        if (input.files && input.files[0]) { // إذا كانت هناك ملفات وتم اختيار ملف واحد على الأقل
            var reader = new FileReader(); //لقراءة محتويات الملف   FileReader
            reader.onload = function (e) {
                document.getElementById(img).src = e.target.result; // تحتوى على البيانات التي تم قراءتها من الملف بعد نجاح عملية القراءة
            }
            reader.readAsDataURL(input.files[0]); // قراءة محتويات الملف المحدد وتحويلها إلى تنسيق مناسب لعرضه كمعاينة
        }
        
        document.getElementById(isThereImg).value = 'yes';
        document.getElementById(defaultText).innerHTML = '';

        {
            //document.getElementById(isThereImg).value = file.name;
            /*var imageUrl = document.getElementById('isThereImg');
            imageUrl.value = 'img';*/
            //document.getElementById("defaultText").innerHTML = null;
            //document.getElementById(img).src = '~/img/News.png';  // لا يعمل؟
            //alert(input.files[0]); // file(0)
            //alert(input.files); // file list
        }
    }
}

function clearFile(input, img, isThereImg) {
    //alert("test1");
    document.getElementById(input).value = null;
    document.getElementById(img).src = '';
    document.getElementById(isThereImg).value = null;
    return false; //إذا كان البوتون داخل الفورم، فإنه  سيمنع إرسال النموذج
}





//----------زايدة------
// كتابة اسم الملف فقط نستعمله في حال الملف المرفوع ليس صورة /
$(document).ready(function () {
    $('.custom-file-input').on("change", function () {
        var fileName = $(this).val().split("\\").pop();
        $(this).next('.custom-file-label').html(fileName);
    });
});
