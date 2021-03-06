﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Tasks;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Menu;

namespace Mitbg.Plugin.Misc.VendorsExtensions
{
    public partial class VendorsExtensionsPlugin : BasePlugin, IAdminMenuPlugin
    {

        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly IWorkContext _workContext;

        public VendorsExtensionsPlugin(ILocalizationService localizationService, IPermissionService permissionService, IWorkContext workContext)
        {
            _localizationService = localizationService;
            _permissionService = permissionService;
            _workContext = workContext;
        }

        public void ManageSiteMap(SiteMapNode rootNode)
        {
            var ordersNode = rootNode.ChildNodes.Single(w => w.SystemName == "Sales");

            var perm = _permissionService.GetPermissionRecordBySystemName("VendorComissions");
            var visible = _workContext.CurrentCustomer.CustomerRoles.SelectMany(s => s.PermissionRecordCustomerRoleMappings.Where(w => w.PermissionRecordId == perm.Id)).Any();

            var subMenuItem = new SiteMapNode()
            {
                SystemName = "VendorComissions",
                Title = _localizationService.GetResource("VendorPercentage.MenuTitle"),
                ControllerName = "ComissionCalculator",
                ActionName = "VendorsList",
                IconClass = "fa-dot-circle-o",
                Visible = visible,
                RouteValues = new RouteValueDictionary() { { "area", "Admin" } },
            };

            var subMenuEditorItem = new SiteMapNode()
            {
                SystemName = "VendorComissionsEditor",
                Title = _localizationService.GetResource("VendorComissionsEditor.MenuTitle"),
                ControllerName = "ComissionsConfigure",
                ActionName = "ComissionsList",
                IconClass = "fa-dot-circle-o",
                Visible = visible,
                RouteValues = new RouteValueDictionary() { { "area", "Admin" } },
            };

            ordersNode.ChildNodes.Add(subMenuItem);
            ordersNode.ChildNodes.Add(subMenuEditorItem);
        }


        public override void Install()
        {
            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.MenuTitle", "Търговци комисионна");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.Title", "Търговци комисионна");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.DateFrom", "От дата");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.DateFrom.hint", "Датата включва всички поръчки със статус Завършена или Отказана - търговец. ");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.DateTo", "До дата");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.DateTo.hint", "Датата включва всички поръчки със статус Завършена или Отказана - търговец.");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.Dates.hint", "Датите включва всички поръчки със статус Завършена или Отказана - търговец.");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.VendorName", "Име");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.VendorId", "ID");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.VendorComissionPercentage", "Комисионна %");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.VendorTotalAmountOfSales", "Оборот за периода");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.VendorComission", "Комисионна");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.VendorTransaction", "За превод");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.FreeShippingSum", "Безплатна доставка");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorPercentage.VendorsList.CodComissionSum", "Наложен платеж");


            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.MenuTitle", "Настройка на Комисията");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.ComissionsList.Filter.Vendor", "Търговец");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.ComissionsList.Filter.Category", "Категория");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.ComissionsList.Vendor", "Търговец");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.ComissionsList.Category", "Категория");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.ComissionsList.Comission", "Комисионна");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.ComissionsList.BasicComission", "Базова комисионна");

            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.Vendor", "Търговец");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.AllVendors", "Всички търговци");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.Category", "Категория");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.AllCategories", "Всички категории");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.Comission", "Комисионна");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.AddNew", "Добавяне на ново");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.Edit", "Изменят");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.BackToList", "назад към списъка");
            _localizationService.AddOrUpdatePluginLocaleResource("VendorComissionsEditor.Editor.Deleted", "Комисията е премахната");




            _permissionService.InstallPermissions(new Permission());

            base.Install();
        }

        public override void Uninstall()
        {
            //locales
            _localizationService.DeletePluginLocaleResource("VendorPercentage.MenuTitle");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.Title");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.DateFrom");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.DateFrom.hint");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.DateTo");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.DateTo.hint");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.Dates.hint");

            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.VendorName");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.VendorId");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.VendorComissionPercentage");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.VendorTotalAmountOfSales");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.VendorComission");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.VendorTransaction");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.FreeShippingSum");
            _localizationService.DeletePluginLocaleResource("VendorPercentage.VendorsList.CodComissionSum");

            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.MenuTitle");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.ComissionsList.Filter.Vendor");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.ComissionsList.Filter.Category");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.ComissionsList.Vendor");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.ComissionsList.Category");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.ComissionsList.Comission");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.ComissionsList.BasicComission");

            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.Vendor");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.AllVendors");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.Category");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.AllCategories");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.Comission");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.AddNew");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.Edit");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.BackToList");
            _localizationService.DeletePluginLocaleResource("VendorComissionsEditor.Editor.Deleted");


            _permissionService.UninstallPermissions(new Permission());

            base.Uninstall();

        }
    }
}
