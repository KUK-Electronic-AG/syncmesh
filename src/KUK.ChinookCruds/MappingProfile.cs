using AutoMapper;
using KUK.ChinookCruds.ViewModels;

namespace KUK.ChinookCruds
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Old Schema Mappings
            CreateMap<KUK.Common.ModelsOldSchema.Invoice, OldInvoiceDetailsViewModel>()
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

            CreateMap<OldCustomerEditViewModel, KUK.Common.ModelsOldSchema.Customer>();
            CreateMap<KUK.Common.ModelsOldSchema.Customer, OldCustomerEditViewModel>();

            CreateMap<KUK.Common.ModelsOldSchema.Invoice, OldInvoiceEditViewModel>();
            CreateMap<OldInvoiceEditViewModel, KUK.Common.ModelsOldSchema.Invoice>();

            CreateMap<OldCustomerCreateViewModel, KUK.Common.ModelsOldSchema.Customer>();

            CreateMap<OldInvoiceCreateViewModel, KUK.Common.ModelsOldSchema.Invoice>();
            CreateMap<OldInvoiceLineCreateViewModel, KUK.Common.ModelsOldSchema.InvoiceLine>();

            // New Schema Mappings
            CreateMap<KUK.Common.ModelsNewSchema.Customer, NewCustomerEditViewModel>().ReverseMap();
            CreateMap<NewCustomerCreateViewModel, KUK.Common.ModelsNewSchema.Customer>().ReverseMap();

            CreateMap<KUK.Common.ModelsNewSchema.Invoice, NewInvoiceDetailsViewModel>()
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

            CreateMap<NewInvoiceEditViewModel, KUK.Common.ModelsNewSchema.Invoice>()
                .ForMember(dest => dest.InvoiceLines, opt => opt.MapFrom(src => src.InvoiceLines.Select(il => new KUK.Common.ModelsNewSchema.InvoiceLine
                {
                    InvoiceLineId = il.InvoiceLineId,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity
                }).ToList()));

            CreateMap<KUK.Common.ModelsNewSchema.Invoice, NewInvoiceEditViewModel>()
                .ForMember(dest => dest.InvoiceLines, opt => opt.MapFrom(src => src.InvoiceLines.Select(il => new NewInvoiceLineViewModel
                {
                    InvoiceLineId = il.InvoiceLineId,
                    InvoiceId = il.InvoiceId,
                    TrackId = il.TrackId,
                    UnitPrice = il.UnitPrice,
                    Quantity = il.Quantity
                }).ToList()));

            CreateMap<NewInvoiceCreateViewModel, KUK.Common.ModelsNewSchema.Invoice>()
                .ForMember(dest => dest.InvoiceDate, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.InvoiceDate, DateTimeKind.Utc)))
                .ReverseMap();
            CreateMap<NewInvoiceLineCreateViewModel, KUK.Common.ModelsNewSchema.InvoiceLine>();

            // Address Mappings
            CreateMap<KUK.Common.ModelsNewSchema.Address, NewAddressDetailsViewModel>()
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

            CreateMap<NewAddressCreateViewModel, KUK.Common.ModelsNewSchema.Address>().ReverseMap();
            CreateMap<NewAddressEditViewModel, KUK.Common.ModelsNewSchema.Address>().ReverseMap();
        }
    }
}
