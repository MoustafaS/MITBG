﻿using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.ePay.Models;
using Nop.Plugin.Shipping.Speedy;
using Nop.Plugin.Shipping.Speedy.Models;
using Nop.Services.Orders;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.ePay.Components
{
    [ViewComponent(Name = "PaymentEpay")]
    public class PaymentEpayComponent : NopViewComponent
    {
        private readonly EPayPaymentSettings _ePayPaymentSettings;
        private readonly ISpeedyContext _speedyContext;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IShoppingCartService _shoppingCartService;

        public PaymentEpayComponent(EPayPaymentSettings ePayPaymentSettings, ISpeedyContext speedyContext, IWorkContext workContext, IStoreContext storeContext, IShoppingCartService shoppingCartService)
        {
            _ePayPaymentSettings = ePayPaymentSettings;
            _speedyContext = speedyContext;
            _workContext = workContext;
            _storeContext = storeContext;
            _shoppingCartService = shoppingCartService;
        }

        public IViewComponentResult Invoke()
        {
            var customer = _workContext.CurrentCustomer;
            var bol = _speedyContext.GetBillInfo(customer.Id);
            var cart = _shoppingCartService.GetShoppingCart(_workContext.CurrentCustomer, ShoppingCartType.ShoppingCart, _storeContext.CurrentStore.Id);
            var totalSum = cart.Sum(w => w.Product.Price * w.Quantity);
            var sumWithComission = _ePayPaymentSettings.AdditionalFeePercentage
                ? totalSum + (totalSum / 100 * _ePayPaymentSettings.AdditionalFee)
                : totalSum + _ePayPaymentSettings.AdditionalFee;

            var model = new EpayPaymentMethodModel
            {
                EnableEpay = _ePayPaymentSettings.EnableEpay,
                EnableEasyPay = _ePayPaymentSettings.EnableEasyPay,
                EnableDirectCreditCard = _ePayPaymentSettings.EnableDirectCreditCard,
                EnableCashOnDelivery = _ePayPaymentSettings.EnableCashOnDelivery && (bol.DeliveryOption != DeliveryOption.Automat || sumWithComission <= 2000)
            };

            if (_ePayPaymentSettings.EnableEpay)
            {
                model.PaymentType = PaymentType.Epay;
            }
            else
            {
                model.PaymentType = PaymentType.EasyPay;
            }

            return View("~/Plugins/Payments.ePay/Views/PaymentEpay/PaymentInfo.cshtml", model);
        }
    }
}