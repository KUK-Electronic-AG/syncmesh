using AutoMapper;
using KUK.ChinookCrudsWebApp.ViewModels;

namespace KUK.ChinookCrudsWebApp
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Old Schema Mappings
            CreateMap<KUK.ChinookSync.Models.OldSchema.Invoice, OldInvoiceDetailsViewModel>()
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.Customer.FirstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.Customer.LastName))
                .ForMember(dest => dest.InvoiceLines, opt => opt.MapFrom(src => src.InvoiceLines.Select(il => new OldInvoiceLineViewModel
                {
                    InvoiceLineId = il.InvoiceLineId,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity
                }).ToList()));

            CreateMap<OldCustomerEditViewModel, KUK.ChinookSync.Models.OldSchema.Customer>();
            CreateMap<KUK.ChinookSync.Models.OldSchema.Customer, OldCustomerEditViewModel>();

            CreateMap<KUK.ChinookSync.Models.OldSchema.Invoice, OldInvoiceEditViewModel>();
            CreateMap<OldInvoiceEditViewModel, KUK.ChinookSync.Models.OldSchema.Invoice>();

            CreateMap<OldCustomerCreateViewModel, KUK.ChinookSync.Models.OldSchema.Customer>();

            CreateMap<OldInvoiceCreateViewModel, KUK.ChinookSync.Models.OldSchema.Invoice>();
            CreateMap<OldInvoiceLineCreateViewModel, KUK.ChinookSync.Models.OldSchema.InvoiceLine>();

            // New Schema Mappings
            CreateMap<KUK.ChinookSync.Models.NewSchema.Customer, NewCustomerEditViewModel>().ReverseMap();
            CreateMap<NewCustomerCreateViewModel, KUK.ChinookSync.Models.NewSchema.Customer>().ReverseMap();

            CreateMap<KUK.ChinookSync.Models.NewSchema.Invoice, NewInvoiceDetailsViewModel>()
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.Customer.FirstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.Customer.LastName))
                .ForMember(dest => dest.InvoiceLines, opt => opt.MapFrom(src => src.InvoiceLines.Select(il => new NewInvoiceLineViewModel
                {
                    InvoiceLineId = il.InvoiceLineId,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity
                }).ToList()));

            CreateMap<NewInvoiceEditViewModel, KUK.ChinookSync.Models.NewSchema.Invoice>()
                .ForMember(dest => dest.InvoiceLines, opt => opt.MapFrom(src => src.InvoiceLines.Select(il => new KUK.ChinookSync.Models.NewSchema.InvoiceLine
                {
                    InvoiceLineId = il.InvoiceLineId,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity
                }).ToList()));

            CreateMap<KUK.ChinookSync.Models.NewSchema.Invoice, NewInvoiceEditViewModel>()
                .ForMember(dest => dest.InvoiceLines, opt => opt.MapFrom(src => src.InvoiceLines.Select(il => new NewInvoiceLineViewModel
                {
                    InvoiceLineId = il.InvoiceLineId,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity
                }).ToList()));

            CreateMap<NewInvoiceCreateViewModel, KUK.ChinookSync.Models.NewSchema.Invoice>()
                .ForMember(dest => dest.InvoiceDate, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.InvoiceDate, DateTimeKind.Utc)))
                .ReverseMap();
            CreateMap<NewInvoiceLineCreateViewModel, KUK.ChinookSync.Models.NewSchema.InvoiceLine>();

            // Address Mappings
            CreateMap<KUK.ChinookSync.Models.NewSchema.Address, NewAddressDetailsViewModel>()
                .ForMember(dest => dest.Customers, opt => opt.MapFrom(src => src.Customers.Select(c => new NewCustomerViewModel
                {
                    CustomerId = c.CustomerId,
                    FirstName = c.FirstName,
                    LastName = c.LastName
                }).ToList()))
                .ForMember(dest => dest.Invoices, opt => opt.MapFrom(src => src.Invoices.Select(i => new NewInvoiceViewModel
                {
                    InvoiceId = i.InvoiceId,
                    InvoiceDate = i.InvoiceDate,
                    Total = i.Total
                }).ToList()))
                .ReverseMap();

            CreateMap<NewAddressCreateViewModel, KUK.ChinookSync.Models.NewSchema.Address>().ReverseMap();
            CreateMap<NewAddressEditViewModel, KUK.ChinookSync.Models.NewSchema.Address>().ReverseMap();
        }
    }
}
