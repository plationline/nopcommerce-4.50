using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tax;
using Po.Requests.Authorization.Objects;

namespace Nop.Plugin.Payments.PlatiOnline
{
    /// <summary>
    /// PayPalStandard payment processor
    /// </summary>
    public class PlatiOnlinePaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ICountryService _countryService;
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        //private readonly IUrlHelperFactory _urlHelperFactory;
        //private readonly IActionContextAccessor _actionContextAccessor;
        private readonly PlatiOnlinePaymentSettings _platiOnlinePaymentSettings;

        #endregion

        #region Ctor

        public PlatiOnlinePaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            IWorkContext workContext,
            IUrlHelperFactory urlHelperFactory,
            IActionContextAccessor actionContextAccessor,
            ICustomerService customerService,
            IStateProvinceService stateProvinceService,
            ICountryService countryService,
            IOrderService orderService,
            IProductService productService,
            PlatiOnlinePaymentSettings platiOnlinePaymentSettings)
        {
            _currencySettings = currencySettings;
            _checkoutAttributeParser = checkoutAttributeParser;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _taxService = taxService;
            _webHelper = webHelper;
            _platiOnlinePaymentSettings = platiOnlinePaymentSettings;
            _workContext = workContext;
            _customerService = customerService;
            _stateProvinceService = stateProvinceService;
            _countryService = countryService;
            _orderService = orderService;
            _productService = productService;
        }

        #endregion

        #region Utilities

        public string ObjectIsNullOrEmpty(object obj)
        {
            string rez = "-";
            if (obj != null)
            {
                if (obj.ToString().Trim() != "")
                {
                    rez = obj.ToString().Trim();
                }
            }
            return rez;
        }

        public string PhoneObjectIsNullOrEmpty(object obj)
        {
            string rez = "-";
            if (obj != null)
            {
                if (obj.ToString().Trim() != "")
                {
                    rez = obj.ToString().Trim();
                }
            }
            if (rez.Length < 10) rez = "0000000000";
            return rez;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the process payment result
        /// </returns>
        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return Task.FromResult(new ProcessPaymentResult());
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            #region process_currency

            string curentCurencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;

            if (_platiOnlinePaymentSettings.RON == true && curentCurencyCode == "RON")
                await _workContext.SetWorkingCurrencyAsync(await _currencyService.GetCurrencyByCodeAsync("RON"));
            else
                if (_platiOnlinePaymentSettings.EUR == true && curentCurencyCode == "EUR")
                await _workContext.SetWorkingCurrencyAsync(await _currencyService.GetCurrencyByCodeAsync("EUR"));
            else
                if (_platiOnlinePaymentSettings.USD == true && curentCurencyCode == "USD")
                await _workContext.SetWorkingCurrencyAsync(await _currencyService.GetCurrencyByCodeAsync("USD"));
            else
                await _workContext.SetWorkingCurrencyAsync(await _currencyService.GetCurrencyByCodeAsync(_platiOnlinePaymentSettings.Curency.ToString()));

            #endregion

            Customer customer = await _customerService.GetCustomerByIdAsync(postProcessPaymentRequest.Order.CustomerId);          
            Address customerBillingAddress = await _customerService.GetCustomerBillingAddressAsync(customer);
            Country customersBillingCountry = await _countryService.GetCountryByIdAsync((int)customerBillingAddress.CountryId);
            StateProvince cusomersBillingState = await _stateProvinceService.GetStateProvinceByIdAsync((int)customerBillingAddress.StateProvinceId);
            Address customerShippingAddress = await _customerService.GetCustomerShippingAddressAsync(customer);
            Country customersShippingCountry = await _countryService.GetCountryByIdAsync((int)customerShippingAddress.CountryId);
            StateProvince customersShippingState = await _stateProvinceService.GetStateProvinceByIdAsync((int)customerShippingAddress.StateProvinceId);
            IList<OrderItem> orderItems = await _orderService.GetOrderItemsAsync(postProcessPaymentRequest.Order.Id);

            Po.Po po = new Po.Po();

            #region Merchant settings

            po.merchant_f_login = _platiOnlinePaymentSettings.Merchant_Id;
            po.merchant_ivAuth = _platiOnlinePaymentSettings.IvAuth;
            po.merchant_publicKey = _platiOnlinePaymentSettings.Public_Key;
            po.merchant_relay_response_f_relay_response_url = _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL) + _platiOnlinePaymentSettings.Relay_Response_URL;
            po.merchant_relay_response_f_relay_method = _platiOnlinePaymentSettings.RelayMethod.ToString();
            //po.log_path = @"";

            #endregion

            #region set_authorization_fields

            po.Authorization.f_amount = (await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(postProcessPaymentRequest.Order.OrderTotal, (await _workContext.GetWorkingCurrencyAsync()))).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
            po.Authorization.f_currency = ObjectIsNullOrEmpty((await _workContext.GetWorkingCurrencyAsync())?.CurrencyCode);
            po.Authorization.f_language = ObjectIsNullOrEmpty((await _workContext.GetWorkingLanguageAsync())?.UniqueSeoCode);
            po.Authorization.f_order_number = ObjectIsNullOrEmpty(postProcessPaymentRequest.Order.Id);
            po.Authorization.f_test_request = Convert.ToInt16(_platiOnlinePaymentSettings.TestMode).ToString();
            po.Authorization.f_order_string = "Plata comenzii cu id " + po.Authorization.f_order_number + " pe site-ul " + _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL);
            po.Authorization.f_website = _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL);

            #region card holder info

            po.Authorization.card_holder_info.same_info_as = "0";

            #region contact

            po.Authorization.card_holder_info.contact.f_email = ObjectIsNullOrEmpty(customerBillingAddress.Email);
            po.Authorization.card_holder_info.contact.f_phone = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.card_holder_info.contact.f_mobile_number = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.card_holder_info.contact.f_send_sms = "1";
            po.Authorization.card_holder_info.contact.f_first_name = ObjectIsNullOrEmpty(customerBillingAddress.FirstName);
            po.Authorization.card_holder_info.contact.f_last_name = ObjectIsNullOrEmpty(customerBillingAddress.LastName);

            #endregion

            #region address

            po.Authorization.card_holder_info.address.f_company = ObjectIsNullOrEmpty(customerBillingAddress.Company);
            po.Authorization.card_holder_info.address.f_zip = ObjectIsNullOrEmpty(customerBillingAddress.ZipPostalCode);
            po.Authorization.card_holder_info.address.f_country = customersBillingCountry.Name != null ? ObjectIsNullOrEmpty(customersBillingCountry.Name) : "Romania";
            po.Authorization.card_holder_info.address.f_state = cusomersBillingState != null ? ObjectIsNullOrEmpty(cusomersBillingState.Name) : "-";
            po.Authorization.card_holder_info.address.f_city = ObjectIsNullOrEmpty(customerBillingAddress.City);
            po.Authorization.card_holder_info.address.f_address = ObjectIsNullOrEmpty(customerBillingAddress.Address1);

            #endregion

            #endregion

            #region customer_info

            #region contact
            po.Authorization.customer_info.contact.f_email = ObjectIsNullOrEmpty(customerBillingAddress.Email);
            po.Authorization.customer_info.contact.f_phone = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.customer_info.contact.f_mobile_number = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.customer_info.contact.f_send_sms = "1";
            po.Authorization.customer_info.contact.f_first_name = ObjectIsNullOrEmpty(customerBillingAddress.FirstName);
            po.Authorization.customer_info.contact.f_last_name = ObjectIsNullOrEmpty(customerBillingAddress.LastName);
            #endregion

            #region invoice
            po.Authorization.customer_info.invoice.f_company = ObjectIsNullOrEmpty(customerBillingAddress.Company);
            po.Authorization.customer_info.invoice.f_cui = "-";
            po.Authorization.customer_info.invoice.f_reg_com = "-";
            po.Authorization.customer_info.invoice.f_cnp = "-";
            po.Authorization.customer_info.invoice.f_zip = ObjectIsNullOrEmpty(customerBillingAddress.ZipPostalCode);
            po.Authorization.customer_info.invoice.f_country = customersBillingCountry.Name != null ? ObjectIsNullOrEmpty(customersBillingCountry.Name) : "Romania";
            po.Authorization.customer_info.invoice.f_state = cusomersBillingState != null ? ObjectIsNullOrEmpty(cusomersBillingState.Name) : "-";
            po.Authorization.customer_info.invoice.f_city = ObjectIsNullOrEmpty(customerBillingAddress.City);
            po.Authorization.customer_info.invoice.f_address = ObjectIsNullOrEmpty(customerBillingAddress.Address1);
            #endregion

            #endregion

            #region order_cart

            #region items

            foreach (var orderItem in orderItems)
            {
                var itemPriceExclTax = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(orderItem.UnitPriceExclTax, await _workContext.GetWorkingCurrencyAsync());
                var itemPriceInclTax = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(orderItem.UnitPriceInclTax, await _workContext.GetWorkingCurrencyAsync());

                var product = await _productService.GetProductByIdAsync(orderItem.ProductId);

                item item = new item();
                item.prodid = orderItem.ProductId.ToString();
                item.name = ObjectIsNullOrEmpty(product.Name);
                item.description = ObjectIsNullOrEmpty(product.ShortDescription);
                item.qty = ObjectIsNullOrEmpty(orderItem.Quantity);
                item.itemprice = itemPriceExclTax.ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
                item.vat = (orderItem.Quantity * (itemPriceInclTax - itemPriceExclTax)).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
                item.stamp = DateTime.Now.ToString("yyyy-MM-dd");
                item.prodtype_id = orderItem.IsDownloadActivated ? "0" : "1";

                po.Authorization.f_order_cart.item.Add(item);
            }

            #endregion

            #region coupons

            Po.Requests.Authorization.Objects.coupon coupon = new Po.Requests.Authorization.Objects.coupon();

            coupon.key = postProcessPaymentRequest.Order.Id.ToString();
            coupon.value = (await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(postProcessPaymentRequest.Order.OrderSubTotalDiscountExclTax, await _workContext.GetWorkingCurrencyAsync())).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
            coupon.percent = "1";
            coupon.workingname = "Discount Code";
            coupon.type = "0";
            coupon.scop = "0";
            coupon.vat = (await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(postProcessPaymentRequest.Order.OrderSubTotalDiscountInclTax - postProcessPaymentRequest.Order.OrderSubTotalDiscountExclTax,await _workContext.GetWorkingCurrencyAsync())).ToString("F", CultureInfo.CreateSpecificCulture("en-US")); ;


            po.Authorization.f_order_cart.coupon.Add(coupon);


            #endregion

            #region shipping

            po.Authorization.f_order_cart.shipping.name = postProcessPaymentRequest.Order.ShippingMethod;
            po.Authorization.f_order_cart.shipping.price = (await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(postProcessPaymentRequest.Order.OrderShippingExclTax, await _workContext.GetWorkingCurrencyAsync())).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
            po.Authorization.f_order_cart.shipping.pimg = "-";
            po.Authorization.f_order_cart.shipping.vat = (await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(postProcessPaymentRequest.Order.OrderShippingInclTax - postProcessPaymentRequest.Order.OrderShippingExclTax, await _workContext.GetWorkingCurrencyAsync())).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));

            #endregion

            #endregion

            #region shipping info

            po.Authorization.shipping_info.same_info_as = "0";

            #region contact

            po.Authorization.shipping_info.contact.f_email = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.Email) : "xxx@xxx.com";
            po.Authorization.shipping_info.contact.f_phone = customerShippingAddress != null ? PhoneObjectIsNullOrEmpty(customerShippingAddress.PhoneNumber) : "0000000000";
            po.Authorization.shipping_info.contact.f_mobile_number = customerShippingAddress != null ? PhoneObjectIsNullOrEmpty(customerShippingAddress.PhoneNumber) : "0000000000";
            po.Authorization.shipping_info.contact.f_send_sms = customerShippingAddress != null ? "1" : "0";
            po.Authorization.shipping_info.contact.f_first_name = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.FirstName) : "-";
            po.Authorization.shipping_info.contact.f_last_name = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.LastName) : "-";

            #endregion

            #region address

            po.Authorization.shipping_info.address.f_company = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.Company) : "-";
            po.Authorization.shipping_info.address.f_zip = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.ZipPostalCode) : "-";
            po.Authorization.shipping_info.address.f_country = customersShippingCountry != null && customersShippingCountry.Name != null ? ObjectIsNullOrEmpty(customersShippingCountry.Name) : "Romania";
            po.Authorization.shipping_info.address.f_state = customersShippingState != null && customersShippingState.Name != null ? ObjectIsNullOrEmpty(customersShippingState.Name) : "-";
            po.Authorization.shipping_info.address.f_city = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.City) : "-";
            po.Authorization.shipping_info.address.f_address = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.Address1) : "-";

            #endregion

            #endregion

            #endregion
            
            try
            {
                #region authorization_request

                po_auth_url_response po_auth_url_response = po.Authorization.Request<po_auth_url_response>();

                #endregion

                #region process_authorization_response

                if (!po.Authorization.HasError)
                {
                    if (po_auth_url_response.po_error_code == "0")
                    {
                        _httpContextAccessor.HttpContext.Response.Redirect(po_auth_url_response.po_redirect_url);

                    }
                    else
                    {
                        var redirectUrl = "../PaymentPlatiOnline/CheckoutCompleted?orderId=" + postProcessPaymentRequest.Order.Id + "&error=" + HttpUtility.UrlEncode(po_auth_url_response.po_error_reason);

                        //ensure redirect URL doesn't exceed 2K chars to avoid "too long URL" exceptionsss
                        if (redirectUrl.Length <= 2048)
                        {
                            _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                            return;
                        }
                    }
                }
                else
                {
                    var redirectUrl = "../PaymentPlatiOnline/CheckoutCompleted?orderId=" + postProcessPaymentRequest.Order.Id + "&error=" + HttpUtility.UrlEncode(po.Authorization.GetError().Error);

                    //ensure redirect URL doesn't exceed 2K chars to avoid "too long URL" exceptionsss
                    if (redirectUrl.Length <= 2048)
                    {
                        _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                        return;
                    }
                }

                #endregion
            }
            catch (Exception e)
            {
                var redirectUrl = "../PaymentPlatiOnline/CheckoutCompleted?orderId=" + postProcessPaymentRequest.Order.Id + "&error=" + HttpUtility.UrlEncode(e.Message);

                //ensure redirect URL doesn't exceed 2K chars to avoid "too long URL" exceptionsss
                if (redirectUrl.Length <= 2048)
                {
                    _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                    return;
                }
            }
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the rue - hide; false - display.
        /// </returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPlatiOnline/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return null;// "Payment.PlatiOnline";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            ////settings
            //await _settingService.SaveSettingAsync(new PlatiOnlinePaymentSettings
            //{
            //    TransactMode = TransactMode.Pending
            //});


            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.MerchantId", "Plati Online Merchant Id");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.MerchantId.Hint", "Specify merchant id.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PublicKey", "Mercahnt PublicKey");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PublicKey.Hint", "Specify merchant public key.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PrivateKey", "Merchant PrivateKey");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PrivateKey.Hint", "Specify merchant private key.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvAuth", "Merchant IvAuth");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvAuth.Hint", "Specify merchant IvAuth.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvItsn", "Merchant IvItsn");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvItsn.Hint", "Specify merchant IvItsn.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactMode", "After checkout mark payment as");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactMode.Hint", "Specify transaction mode.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RON", "Accepted RON ");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RON.Hint", "Specify accepted RON currency.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.EUR", "Accepted EUR ");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.EUR.Hint", "Specify accepted EUR currency.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.USD", "Accepted USD ");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.USD.Hint", "Specify accepted USD currency.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.OtherCurrency", "Other currency ");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.OtherCurrency.Hint", "Specify the currency to replace the other unsupported currencies.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL", "Relay response URL");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL.Hint", "Specify the URL address to which the PO server will send the response for the transactions made by your clients");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RelayMethod", "Relay method ");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RelayMethod.Hint", "Specify the method.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TestMode", "Test mode");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TestMode.Hint", "Specify test mode.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.SSL", "Use SSL");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.SSL.Hint", "Specify use SSL.");

            //used in PaymentInfo.cshtml
            await  _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RedirectionTip", "You will be redirected to PlatiOnline site to complete the payment.", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RedirectionTip", "Veti fi redirectionat catre site-ul PlatiOnline pentru a finaliza plata.", "ro-RO");

            //used in CheckoutCompleted.cshtml
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.WeAreSorry", "We are sorry", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.WeAreSorry", "Ne pare rau", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.YourTransactionIs", "Your transaction is", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.YourTransactionIs", "Tranzactia este", "ro-RO");
            
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReview", "Transaction under security review", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReview", "Tranzactia necesita verificari suplimentare", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReviewInfo", "This transaction is not yet approved, it is under review for security reasons. You will be informed of the outcome of this verification within 1-48 business hours of the date and time of this notification.", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReviewInfo", "Tranzactia nu este inca aprobata si necesita verificari suplimentare. Vei fi anuntat de rezultatul acestor verificari in termen de 1-48 ore.", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.YourOrderHasBeenDeclined", "Your order has been declined", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.YourOrderHasBeenDeclined", "Comanda dumneavoastra a fost refuzata", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionDeclinedInfo", "<p>The transaction was rejected for one of the following reasons: </p><p>  - card information has been incorrectly entered </p><p>  - there are insufficient funds in your account </p><p>  - your card issuing bank rejected the transaction due to security restrictions on the card or card type is not supported</p><p>  - the transaction network is currently unavailable </p><p> We recommend you resubmit the transaction in a few moments, after performing the necessary changes. </p>", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionDeclinedInfo", "<p>Tranzactia a fost refuzata din unul din motivele urmatoare: </p><p>  - datele de card au fost introduse incorect </p><p>  - nu aveti fonduri suficiente </p><p>  - banca dvs. emitenta a refuzat tranzactia datorita unor restrictii de securitate sau tipul de card nu este acceptat </p><p>  - reteaua de procesare nu este disponibila in acest moment </p><p> Iti recomandam sa reincerci efectuarea platii in cateva momenete, dupa ce efectuezi schimbarile necesare. </p>", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionError", "Transaction error", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionError", "A aparaut o eroare in procesare tranzactie", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionErrorInfo", "Please don't resubmit your transaction. Please contact customersupport.", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionErrorInfo", "Va rog sa nu reincercati inainte de a contacta departamentul de releatii cu clientii", "ro-RO");
            
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Authorized", "Authorized", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Authorized", "Autorizata", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.OnHold", "OnHold", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.OnHold", "In asteptare", "ro-RO");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Declined", "Declined", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Declined", "Refuzata", "ro-RO");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PoInfo", "You must log into <a href='https://merchants.plationline.ro/' target='_blank'><b>PlatiOnline</b></a> account, <b>Settings</b> section to get the info you need for the fields below.", "en-US");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PoInfo", "Trebuie sa va logati in contul <a href='https://merchants.plationline.ro/' target='_blank'><b>PlatiOnline</b>, </a>sectiunea Setari pentru a completa datele de mai jos.", "ro-RO");

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<PlatiOnlinePaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.MerchantId");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.MerchantId.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PublicKey");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PublicKey.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PrivateKey");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PrivateKey.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvAuth");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvAuth.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvItsn");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.IvItsn.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactMode");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactMode.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RON");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RON.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.EUR");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.EUR.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.USD");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.USD.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.OtherCurrency");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.OtherCurrency.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RelayMethod");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RelayMethod.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TestMode");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TestMode.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.SSL");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.SSL.Hint");

            //used in PaymentInfo.cshtml
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RedirectionTip");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.RedirectionTip");

            //used in CheckoutCompleted.cshtml
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.WeAreSorry");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.YourTransactionIs");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReview");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReviewInfo");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.YourOrderHasBeenDeclined");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionDeclinedInfo");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionError");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.TransactionErrorInfo");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Authorized");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.OnHold");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.Declined");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PlatiOnline.Fields.PoInfo");

            await base.UninstallAsync();
        }

        public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(0m);
        }

        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            throw new NotImplementedException();
        }

        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PlatiOnline site to complete the payment"
            return await _localizationService.GetResourceAsync("Plugins.Payments.PlatiOnline.Fields.RedirectionTip");
         }

        #endregion
    }
}