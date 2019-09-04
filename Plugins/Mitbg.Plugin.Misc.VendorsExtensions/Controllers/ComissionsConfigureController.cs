﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Mitbg.Plugin.Misc.VendorsCore.Domain.Entities;
using Mitbg.Plugin.Misc.VendorsExtensions.Models;
using Nop.Core.Data;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Vendors;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Customers;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Mitbg.Plugin.Misc.VendorsExtensions.Controllers
{
    public class ComissionsConfigureController : BaseAdminController
    {

        private readonly IPermissionService _permissionService;
        private readonly IRepository<VendorComission> _vendorComissionsRep;
        private readonly IRepository<Vendor> _vendorsRep;
        private readonly IRepository<Category> _categoriessRep;

        public ComissionsConfigureController(IPermissionService permissionService, IRepository<VendorComission> vendorComissionsRep, IRepository<Vendor> vendorsRep, IRepository<Category> categoriessRep)
        {
            _permissionService = permissionService;
            _vendorComissionsRep = vendorComissionsRep;
            _vendorsRep = vendorsRep;
            _categoriessRep = categoriessRep;
        }

        // GET
        [Area(AreaNames.Admin)]
        [HttpGet]
        public IActionResult ComissionsList()
        {
            if (!_permissionService.Authorize(Permission.VendorComission))
                return AccessDeniedView();

            var model = new ComissionsListSearchModel();
            return View("~/Plugins/Mitbg.Plugin.Misc.VendorsExtensions/Views/ComissionsConfigure/ComissionsList.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        public IActionResult ComissionsList(ComissionsListSearchModel searchModel)
        {


            var comissionsQuery = _vendorComissionsRep.Table;

            if (searchModel.VendorId > 0)
                comissionsQuery = comissionsQuery.Where(w => w.VendorId == searchModel.VendorId);

            if (searchModel.CategoryId > 0)
                comissionsQuery = comissionsQuery.Where(w => w.CategoryId == searchModel.CategoryId);

            var totalCount = comissionsQuery.Count();

            var comissions = comissionsQuery.OrderBy(w => w.VendorId).ThenBy(w => w.CategoryId).Skip(searchModel.Start).Take(searchModel.PageSize).ToList();

            var vendorsNames = _vendorsRep.Table.Where(w => comissions.Select(s => s.VendorId).Contains(w.Id)).ToDictionary(w => w.Id, w => w.Name);
            var categoriesNames = _categoriessRep.Table.Where(w => comissions.Where(ww => ww.CategoryId.HasValue).Select(s => s.CategoryId).Contains(w.Id)).ToDictionary(w => w.Id, w => w.Name);

            var model = new ComissionsListModel
            {
                Data = comissions.Select(s => new ComissionConfigureModel
                {
                    Id = s.Id,
                    VendorId = s.VendorId,
                    CategoryId = s.CategoryId,
                    Comission = s.ComissionPercentage,
                    VendorName = vendorsNames.ContainsKey(s.VendorId) ? vendorsNames[s.VendorId] : "",
                    CategoryName = s.CategoryId.HasValue && categoriesNames.ContainsKey(s.CategoryId.Value) ? categoriesNames[s.CategoryId.Value] : ""
                }),
                Total = totalCount,
                RecordsTotal = totalCount,
                RecordsFiltered = totalCount
            };

            return Json(model);
        }

        public virtual IActionResult Create()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCustomers))
                return AccessDeniedView();

            //prepare model
            var model = new VendorComissionCreateEditModel()
            {

                AvailableVendors = _vendorsRep.Table.Where(w => !w.Deleted && w.Active).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList().OrderBy(w => w.Text).ToList(),
                AvailableCategories = _categoriessRep.Table.Where(w => !w.Deleted).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList().OrderBy(w => w.Text).ToList(),
            };
            model.AvailableCategories.Insert(0, new SelectListItem("Базовая", "-1", true));

            model.VendorId = int.Parse(model.AvailableVendors.First().Value);
            model.CategoryId = -1;
            model.ComissionPercentage = 0;

            return View("~/Plugins/Mitbg.Plugin.Misc.VendorsExtensions/Views/ComissionsConfigure/Create.cshtml", model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [FormValueRequired("save", "save-continue")]
        public virtual IActionResult Create(VendorComissionCreateEditModel model, bool continueEditing, IFormCollection form)
        {
            if (!_permissionService.Authorize(Permission.VendorComission))
                return AccessDeniedView();

            var existsComission = _vendorComissionsRep.Table.FirstOrDefault(w =>
                w.VendorId == model.VendorId &&
                w.CategoryId == (model.CategoryId <= 0 ? (int?)null : model.CategoryId));

            if (existsComission != null)
                ModelState.AddModelError(string.Empty, "Comission with same params already exists");
            else
            {
                var newComission = new VendorComission()
                {
                    ComissionPercentage = model.ComissionPercentage,
                    VendorId = model.VendorId,
                    CategoryId = model.CategoryId <= 0 ? (int?)null : model.CategoryId
                };

                _vendorComissionsRep.Insert(newComission);

                if (ModelState.IsValid)
                {
                    if (!continueEditing)
                        return RedirectToAction("ComissionsList");

                    return RedirectToAction("Edit", new { id = newComission.Id });
                }
            }

            return View("~/Plugins/Mitbg.Plugin.Misc.VendorsExtensions/Views/ComissionsConfigure/Create.cshtml", model);
        }


        public virtual IActionResult Edit(int id)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageCustomers))
                return AccessDeniedView();

            //try to get a customer with the specified id
            var comission = _vendorComissionsRep.Table.FirstOrDefault(w => w.Id == id);
            if (comission == null)
                return RedirectToAction("ComissionsList");


            var model = new VendorComissionCreateEditModel()
            {

                AvailableVendors = _vendorsRep.Table.Where(w => !w.Deleted && w.Active).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList().OrderBy(w => w.Text).ToList(),
                AvailableCategories = _categoriessRep.Table.Where(w => !w.Deleted).Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList().OrderBy(w => w.Text).ToList(),
            };
            model.AvailableCategories.Insert(0, new SelectListItem("Базовая", "-1", true));

            model.VendorId = comission.VendorId;
            model.CategoryId = comission.CategoryId ?? -1;
            model.ComissionPercentage = comission.ComissionPercentage;


            return View("~/Plugins/Mitbg.Plugin.Misc.VendorsExtensions/Views/ComissionsConfigure/Edit.cshtml", model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [FormValueRequired("save", "save-continue")]
        public virtual IActionResult Edit(VendorComissionCreateEditModel model, bool continueEditing, IFormCollection form)
        {
            if (!_permissionService.Authorize(Permission.VendorComission))
                return AccessDeniedView();

            var existsComission = _vendorComissionsRep.Table.FirstOrDefault(w =>
                w.VendorId == model.VendorId &&
                w.CategoryId == (model.CategoryId <= 0 ? (int?)null : model.CategoryId));

            if (existsComission == null)
                ModelState.AddModelError(string.Empty, "Comission not found");
            else
            {
                existsComission.ComissionPercentage = model.ComissionPercentage;
                existsComission.VendorId = model.VendorId;
                existsComission.CategoryId = model.CategoryId <= 0 ? (int?)null : model.CategoryId;

                _vendorComissionsRep.Update(existsComission);

                if (ModelState.IsValid)
                {
                    if (!continueEditing)
                        return RedirectToAction("ComissionsList");

                    return RedirectToAction("Edit", new { id = existsComission.Id });
                }
            }

            return View("~/Plugins/Mitbg.Plugin.Misc.VendorsExtensions/Views/ComissionsConfigure/Edit.cshtml", model);
        }
    }
}