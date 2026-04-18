using AutoMapper;
using NewsApp2.Models.Entities;
using NewsApp2.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace NewsApp2.Classes
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<SiteVM,SiteInfo>()
             .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.SiteInfo.Id))
             .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.SiteInfo.Name))
             .ForMember(dest => dest.Activity, opt => opt.MapFrom(src => src.SiteInfo.Activity))
             .ForMember(dest => dest.About, opt => opt.MapFrom(src => src.SiteInfo.About))
             .ForMember(dest => dest.Created, opt => opt.MapFrom(src => src.SiteInfo.Created))
             .ForMember(dest => dest.Modified, opt => opt.MapFrom(_ => DateTime.Now));

            {
                //CreateMap<SiteVM, Contact>()
                // .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Contact.Id))
                // .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Contact.Email))
                // .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Contact.Phone))
                // .ForMember(dest => dest.Facebook, opt => opt.MapFrom(src => src.Contact.Facebook))
                // .ForMember(dest => dest.Twitter, opt => opt.MapFrom(src => src.Contact.Twitter))
                // .ForMember(dest => dest.Instagram, opt => opt.MapFrom(src => src.Contact.Instagram))
                // .ForMember(dest => dest.Created, opt => opt.MapFrom(src => src.Contact.Created))
                // .ForMember(dest => dest.Modified, opt => opt.MapFrom(_ => DateTime.Now));

            }


            //CreateMap<SiteVM, Contact>();  //  ViewModel لا تعمل اذا كانت الحقول داخل كلاس داخل

            CreateMap<SiteVM, Contact>() //ViewModel تحويل جميع الحقول اذا كانت داخل كلاس داخل
                .ConvertUsing<ContactConverter1>();  // ITypeConverter

            //-----------------زايد------------------
            {
                //---------------------------------------------------------------------------
                // ContactVM مثال تجريبي فقط

                CreateMap<ContactVM, Contact>(); // تحويل جميع الحقول من كائن لكائن مباشرة 
                CreateMap<Contact, ContactVM>(); // تحويل جميع الحقول من كائن لكائن مباشرة 


                CreateMap<ContactVM, Contact>().ReverseMap(); // تحويل جميع الحقول من كائن لكائن مباشرة في الإتجاهين 


                CreateMap<ContactVM, Contact>() // تحويل جميع الحقول مع تجاهل حقول معينة
                   .ForMember(dest => dest.Twitter, opt => opt.Ignore())
                   .ForMember(dest => dest.Instagram, opt => opt.Ignore())
                   .ForMember(dest => dest.Created, opt => opt.Ignore());


                CreateMap<ContactVM, Contact>() // تحويل جميع الحقول ولكن بإمكانك التعديل على حقول معينة
                    .AfterMap((src, dest) =>
                    {
                        dest.Email = src.Email.ToUpper();
                        dest.Modified = DateTime.Now;

                    });
                //---------------------------------------------------------------------------
            }
            //--------------------------------------
        }
    }

    public class ContactConverter : ITypeConverter<SiteVM, Contact>
    {
        public Contact Convert(SiteVM source, Contact destination, ResolutionContext context)
        {
            return source.Contact; //Contact ببساطة تم إرجاع
            //المشكلة هنا أن Modified  سيأخذها null
        }
    }

    public class ContactConverter1 : ITypeConverter<SiteVM, Contact>
    {
        public Contact Convert(SiteVM source, Contact destination, ResolutionContext context)
        {
            var contact = source.Contact;
            contact.Email = source.Contact.Email.ToUpper();
            contact.Modified = DateTime.Now;
            return contact;
        }
    }


    //-----------------زايد------------------
    public class ContactVM
    {
        public required string Email { get; set; }
        public required string Phone { get; set; }
        public required string Facebook { get; set; }
        public required string Twitter { get; set; }
        public required string Instagram { get; set; }
    }
    //-----------------------------------------

}
