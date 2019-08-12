﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Data;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Vendors;
using Nop.Plugin.Misc.VendorPercentage.Models;
using Nop.Plugin.Shipping.Speedy.Domain;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using NUglify.Helpers;

namespace Nop.Plugin.Misc.VendorPercentage.Controllers
{

    public partial class ComissionCalculatorController : BaseAdminController
    {
        private readonly IRepository<Vendor> _vendorsRep;
        private readonly IRepository<SpeedyShipment> _speedyShipmentRep;
        private readonly IPermissionService _permissionService;
        private readonly IRepository<OrderNote> _orderNotesRep;
        private readonly IPriceFormatter _priceFormatter;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWorkContext _workContext;
        private readonly IRepository<Shipment> _shipmentRepository;
        private readonly IOrderService _orderService;

        public ComissionCalculatorController(
            IRepository<Vendor> vendorsRep,
            IPriceFormatter priceFormatter,
            IRepository<OrderItem> orderItemsRep,
            IDateTimeHelper dateTimeHelper,
            ICurrencyService currencyService,
            CurrencySettings currencySettings,
            IWorkContext workContext,
            IRepository<OrderNote> orderNotesRep,
            IPermissionService permissionService,
            IRepository<SpeedyShipment> speedyShipmentRep,
            IRepository<Shipment> shipmentRepository, 
            IOrderService orderService)
        {
            _vendorsRep = vendorsRep;
            _priceFormatter = priceFormatter;
            _dateTimeHelper = dateTimeHelper;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _workContext = workContext;
            _orderNotesRep = orderNotesRep;
            _permissionService = permissionService;
            _speedyShipmentRep = speedyShipmentRep;
            _shipmentRepository = shipmentRepository;
            _orderService = orderService;
        }



        [Area(AreaNames.Admin)]
        [HttpGet]
        public IActionResult VendorsList()
        {
            if (!_permissionService.Authorize(Permission.VendorComission))
                return AccessDeniedView();

            var model = new VendorsComissionSearchModel();
            return View("~/Plugins/Misc.VendorPercentage/Views/VendorsList.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        public IActionResult VendorsList(VendorsComissionSearchModel searchModel)
        {
            if (!_permissionService.Authorize(Permission.VendorComission))
                return AccessDeniedDataTablesJson();

            var vendorsQuery = _vendorsRep.Table.Where(w => w.Active).Select(s => new VendorComissionModel
            {
                VendorId = s.Id,
                VendorName = s.Name,
                ComissionPercentage = s.VendorNotes.Any()
                    ? decimal.Parse(s.VendorNotes.First().Note.Replace("%", "").Trim())
                    : 0
            });

            if (!string.IsNullOrEmpty(searchModel.VendorName))
                vendorsQuery = vendorsQuery.Where(w => w.VendorName.Contains(searchModel.VendorName));

            vendorsQuery = vendorsQuery.OrderBy(w => w.VendorName);

            var totalCount = vendorsQuery.Count();
            var vendors = vendorsQuery.ToList();

            //var ordersItemsQuery = _orderNotesRep.Table;

            //var startDateValue = !searchModel.DateFrom.HasValue ? null
            //    : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.DateFrom.Value, _dateTimeHelper.CurrentTimeZone);
            //var endDateValue = !searchModel.DateTo.HasValue ? null
            //    : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.DateTo.Value, _dateTimeHelper.CurrentTimeZone).AddDays(1);

            //if (startDateValue.HasValue)
            //    ordersItemsQuery = ordersItemsQuery.Where(w => w.CreatedOnUtc >= startDateValue);

            //if (endDateValue.HasValue)
            //    ordersItemsQuery = ordersItemsQuery.Where(w => w.CreatedOnUtc <= endDateValue);


            //var oiq = ordersItemsQuery.Where(w => w.Note == "Order status has been edited. New status: Завършена" ||
            //                                w.Note == "Order status has been edited. New status: Отказана - Търговец" ||
            //                                w.Note == "Order status has been edited. New status: Cancelled Vendor")
            //                                .AsEnumerable()
            //                                .DistinctBy(x => x.OrderId)
            //                                .ToList();

            //var ordersIds = oiq
            //    .Where(w => !w.Order.Deleted)
            //    .Select(w => w.OrderId)
            //    .ToList();

            //var speedyShipments = _speedyShipmentRep.Table.Where(w => ordersIds.Contains(w.OrderId)).GroupBy(w => w.VendorId).ToDictionary(w => w.Key, w => w);

            //var totalsQuery = oiq.Where(w => !w.Order.Deleted && (w.Order.OrderStatus == OrderStatus.Complete || w.Order.OrderStatus == OrderStatus.CancelledVendor))
            //    .SelectMany(w => w.Order.OrderItems.Where(ww => ww.Product.VendorId > 0));

            var shipmentQuery = _shipmentRepository.Table;

            var startDateValue = !searchModel.DateFrom.HasValue ? null
                : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.DateFrom.Value, _dateTimeHelper.CurrentTimeZone);
            var endDateValue = !searchModel.DateTo.HasValue ? null
                : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.DateTo.Value, _dateTimeHelper.CurrentTimeZone).AddDays(1);

            if (startDateValue.HasValue)
                shipmentQuery = shipmentQuery.Where(w => w.Order.CreatedOnUtc >= startDateValue);

            if (endDateValue.HasValue)
                shipmentQuery = shipmentQuery.Where(w => w.Order.CreatedOnUtc <= endDateValue);

            var shipments = shipmentQuery.Where(a => !a.Order.Deleted && (a.AdminComment == "1" || a.AdminComment == "2")).ToList();

            var trackNumbers = shipments.Select(x => x.TrackingNumber);
            var speedyShipments = _speedyShipmentRep.Table.Where(w => trackNumbers.Contains(w.BarCode)).GroupBy(w => w.VendorId).ToDictionary(w => w.Key, w => w);

            //admin commnet 1 = shipment complete; admin comment 2 = shipment canceled by vendor
            //var totalsQuery = oiq.SelectMany(x => x.Order.Shipments.Where(a => !a.Order.Deleted && (a.AdminComment == "1" || a.AdminComment == "2"))
            //.SelectMany(w => w.Order.OrderItems.Where(ww => ww.Product.VendorId > 0)));

            var totalsQuery = shipments.SelectMany(x =>
                x.ShipmentItems.Select(s => _orderService.GetOrderItemById(s.OrderItemId)));

            var vendorTotals = totalsQuery.GroupBy(w => w.Product.VendorId)
                .ToDictionary(w => w.Key, w => w.Sum(s => s.PriceExclTax));

            var primaryStoreCurrency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);

            vendors.ForEach(w =>
            {
                w.TotalSum = vendorTotals.ContainsKey(w.VendorId) ? vendorTotals[w.VendorId] : 0;
                w.TotalShippingSum = speedyShipments.ContainsKey(w.VendorId) ? speedyShipments[w.VendorId].Sum(ww => ww.ShippingCost) : 0;
                w.FreeShippingSum = speedyShipments.ContainsKey(w.VendorId) ? speedyShipments[w.VendorId].Where(ww => ww.IsFreeShipping).Sum(ww => ww.ShippingCost + ww.CodComission) : 0;
                w.TotalSumText = _priceFormatter.FormatPrice(w.TotalSum, true, primaryStoreCurrency, _workContext.WorkingLanguage, false, false);
                w.FreeShippingSumText = _priceFormatter.FormatPrice(w.FreeShippingSum, true, primaryStoreCurrency, _workContext.WorkingLanguage, false, false);
                w.ComissionText = _priceFormatter.FormatPrice(w.Comission, true, primaryStoreCurrency, _workContext.WorkingLanguage, false, false);
                w.TransactionText = _priceFormatter.FormatPrice(w.Transaction, true, primaryStoreCurrency, _workContext.WorkingLanguage, false, false);
            });

            var model = new VendorsListModel
            {
                Data = vendors,
                Total = totalCount,
                RecordsTotal = totalCount,
                RecordsFiltered = totalCount
            };

            return Json(model);
        }
    }
}