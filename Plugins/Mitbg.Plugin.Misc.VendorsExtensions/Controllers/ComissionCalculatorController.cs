﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Mitbg.Plugin.Misc.VendorsExtensions.Models;
using Mitbg.Plugin.Misc.VendorsCore.Domain.Entities;
using Nop.Core;
using Nop.Core.Data;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Vendors;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Filters;

namespace Mitbg.Plugin.Misc.VendorsExtensions.Controllers
{

    public partial class ComissionCalculatorController : BaseAdminController
    {
        private readonly IRepository<Vendor> _vendorsRep;
        private readonly IRepository<Category> _categoriesRep;
        private readonly IRepository<ShipmentTask> _shipmentTasksRep;
        private readonly IRepository<VendorComission> _comissionsRep;
        private readonly IPermissionService _permissionService;
        private readonly IRepository<OrderItem> _orderItemsRep;
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
            IPermissionService permissionService,
            IRepository<ShipmentTask> shipmentTasksRep,
            IRepository<Shipment> shipmentRepository,
            IOrderService orderService, IRepository<VendorComission> comissionsRep, IRepository<Category> categoriesRep)
        {
            _vendorsRep = vendorsRep;
            _priceFormatter = priceFormatter;
            _dateTimeHelper = dateTimeHelper;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _workContext = workContext;
            _orderItemsRep = orderItemsRep;
            _permissionService = permissionService;
            _shipmentTasksRep = shipmentTasksRep;
            _shipmentRepository = shipmentRepository;
            _orderService = orderService;
            _comissionsRep = comissionsRep;
            _categoriesRep = categoriesRep;
        }



        [Area(AreaNames.Admin)]
        [HttpGet]
        public IActionResult VendorsList()
        {
            if (!_permissionService.Authorize(Permission.VendorComission))
                return AccessDeniedView();

            var model = new VendorsComissionSearchModel();

            var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            model.DateFrom = firstDayOfMonth;
            model.DateTo = lastDayOfMonth;

            return View("~/Plugins/Mitbg.Plugin.Misc.VendorsExtensions/Views/VendorsList.cshtml", model);
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
                VendorName = s.Name
            });

            if (!string.IsNullOrEmpty(searchModel.VendorName))
                vendorsQuery = vendorsQuery.Where(w => w.VendorName.Contains(searchModel.VendorName));

            vendorsQuery = vendorsQuery.OrderBy(w => w.VendorName);

            var totalCount = vendorsQuery.Count();
            //var vendors = vendorsQuery.ToList();
            var vendors = vendorsQuery.Skip(searchModel.Start).Take(searchModel.PageSize).ToList();

            var vendorIds = vendors.Select(s => s.VendorId).ToList();
            var comissions = _comissionsRep.Table
                .Where(w => vendorIds.Contains(w.VendorId)).ToList();

            var shipmentQuery = _shipmentRepository.Table;

            var startDateValue = !searchModel.DateFrom.HasValue ? null
                : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.DateFrom.Value, _dateTimeHelper.CurrentTimeZone);
            var endDateValue = !searchModel.DateTo.HasValue ? null
                : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.DateTo.Value, _dateTimeHelper.CurrentTimeZone).AddDays(1);

            if (startDateValue.HasValue)
                //shipmentQuery = shipmentQuery.Where(w => w.Order.CreatedOnUtc >= startDateValue);
                shipmentQuery = shipmentQuery
                    .Where(w =>
                    w.Order.Deleted == false &&
                    w.Order.OrderStatus == OrderStatus.Complete &&
                    w.Order.PaidDateUtc >= startDateValue);

            if (endDateValue.HasValue)
                shipmentQuery = shipmentQuery
                    .Where(w =>
                        w.Order.Deleted == false &&
                        w.Order.OrderStatus == OrderStatus.Complete &&
                        w.Order.PaidDateUtc <= endDateValue);
            //shipmentQuery = shipmentQuery.Where(w => w.Order.CreatedOnUtc <= endDateValue);

            var shipments = shipmentQuery.Where(a => !a.Order.Deleted && a.Order.OrderStatus == OrderStatus.Complete && (a.AdminComment == "1" || a.AdminComment == "2")).ToList();

            //transaction
            var shipmentsForTrans = shipmentQuery.Where(a => !a.Order.Deleted && a.Order.OrderStatus == OrderStatus.Complete && (a.AdminComment == "1")).ToList();

            var trackNumbers = shipments.Select(x => x.TrackingNumber);
            var shipmentTasks = _shipmentTasksRep.Table.Where(w => trackNumbers.Contains(w.BarCode)).GroupBy(w => w.VendorId).ToDictionary(w => w.Key, w => w);

            var orderItemsIds = shipments.SelectMany(x => x.ShipmentItems.Select(s => s.OrderItemId)).ToList();
            //transaction
            var orderItemsForTransIds = shipmentsForTrans.SelectMany(x => x.ShipmentItems.Select(s => s.OrderItemId)).ToList();
            var totalsQuery = _orderItemsRep.Table.Where(w => orderItemsIds.Contains(w.Id) && w.Product.VendorId > 0);

            //transaction
            var totalsForTransQuery = _orderItemsRep.Table.Where(w => orderItemsForTransIds.Contains(w.Id) && w.Product.VendorId > 0);

            var primaryStoreCurrency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);
            var categoriesTree = _categoriesRep.Table.Where(w => !w.Deleted).ToList().BuildTree();

            vendors.ForEach(w =>
            {
                var totalSumByCategories = totalsQuery.Where(ww => ww.Product.VendorId == w.VendorId)
                    .GroupBy(g => g.Product.ProductCategories.Select(ss => ss.CategoryId).FirstOrDefault())
                    .ToDictionary(ww => ww.Key, ww => ww.Sum(s => s.PriceExclTax));

                var totalForTransaction = totalsForTransQuery.Where(ww => ww.Product.VendorId == w.VendorId)
                    .GroupBy(g => g.Product.ProductCategories.Select(ss => ss.CategoryId).FirstOrDefault())
                    .ToDictionary(ww => ww.Key, ww => ww.Sum(s => s.PriceExclTax));

                var baseComission = comissions.Where(ww => ww.VendorId == w.VendorId && ww.CategoryId == null).Select(s => s.ComissionPercentage).SingleOrDefault();

                var categoryComissions = GetCategoryComissions(categoriesTree,
                    comissions.Where(ww => ww.VendorId == w.VendorId).ToList(), baseComission);

                var totalComission = totalSumByCategories.Select(s =>
                {
                    var percentage = categoryComissions[s.Key];
                    return s.Value * (percentage / 100m);
                }).Sum(s => s);

                w.Comission = totalComission;
                w.FreeShippingSum = shipmentTasks.ContainsKey(w.VendorId) ? shipmentTasks[w.VendorId].Where(ww => ww.IsFreeShipping).Sum(ww => ww.ShippingCost + ww.CodComission) : 0;

                var totalTrans = totalForTransaction.Select(x => x.Value).Sum();
                var tt = totalTrans - w.Comission - w.FreeShippingSum;
                var totalTransaction = tt > 0 ? tt : 0;

                w.Transaction = totalTransaction;
                w.TotalSum = totalSumByCategories.Sum(s => s.Value);
                w.TotalShippingSum = shipmentTasks.ContainsKey(w.VendorId) ? shipmentTasks[w.VendorId].Sum(ww => ww.ShippingCost + ww.CodComission) : 0;
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

        private Dictionary<int, decimal> GetCategoryComissions(IList<CategoryTreeItem> treeItems, IList<VendorComission> comissions, decimal parentComission)
        {
            var result = new Dictionary<int, decimal>();
            foreach (var item in treeItems)
            {
                var comissionConfig = comissions.SingleOrDefault(w => w.CategoryId == item.Id);
                var comission = comissionConfig == null ? parentComission : comissionConfig.ComissionPercentage;
                result.Add(item.Id, comission);
                foreach (var subItem in GetCategoryComissions(item.Child, comissions, comission))
                {
                    result.Add(subItem.Key, subItem.Value);
                }

            }
            return result;
        }


    }
}