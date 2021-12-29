using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PlatiOnline;
using Nop.Plugin.Payments.PlatiOnline.Models;
using Nop.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Po.Requests.Authorization.Objects;
using Po.Requests.Itsn.Objects;
using Po.Requests.Query.Objects;

namespace Nop.Plugin.Payments.PayPalStandard.Controllers
{
    public class PaymentPlatiOnlineController : BasePaymentController
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPermissionService _permissionService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly INotificationService _notificationService;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly PlatiOnlinePaymentSettings _platiOnlinePaymentSettings;
        private string _contentResult = "";

        #endregion

        #region Ctor

        public PaymentPlatiOnlineController(IWorkContext workContext,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IPermissionService permissionService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            ILogger logger,
            IWebHelper webHelper,
            INotificationService notificationService,
            ShoppingCartSettings shoppingCartSettings,
            PlatiOnlinePaymentSettings platiOnlinePaymentSettings)
        {
            _workContext = workContext;
            _settingService = settingService;
            _paymentService = paymentService;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService;
            _permissionService = permissionService;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _storeContext = storeContext;
            _logger = logger;
            _webHelper = webHelper;
            _notificationService = notificationService;
            _shoppingCartSettings = shoppingCartSettings;
            _platiOnlinePaymentSettings = platiOnlinePaymentSettings;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var platiOnlinePaymentSettings = await _settingService.LoadSettingAsync<PlatiOnlinePaymentSettings>(storeScope);

            var model = new ConfigurationModel()
            {

                Merchant_Id = platiOnlinePaymentSettings.Merchant_Id,
                Public_Key = platiOnlinePaymentSettings.Public_Key,
                Private_Key = platiOnlinePaymentSettings.Private_Key,
                IvItsn = platiOnlinePaymentSettings.IvItsn,
                IvAuth = platiOnlinePaymentSettings.IvAuth,
                RON = platiOnlinePaymentSettings.RON,
                EUR = platiOnlinePaymentSettings.EUR,
                USD = platiOnlinePaymentSettings.USD,
                OtherCurrencyId = Convert.ToInt32(_platiOnlinePaymentSettings.Curency),
                OtherCurrency = await platiOnlinePaymentSettings.Curency.ToSelectListAsync(),
                Relay_Response_URL = platiOnlinePaymentSettings.Relay_Response_URL,
                RelayMethodId = Convert.ToInt32(platiOnlinePaymentSettings.RelayMethod),
                RelayMethod = await platiOnlinePaymentSettings.RelayMethod.ToSelectListAsync(),
                TestMode = platiOnlinePaymentSettings.TestMode,
                SSL = platiOnlinePaymentSettings.SSL,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.Merchant_Id_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.Merchant_Id, storeScope);
                model.Public_Key_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.Public_Key, storeScope);
                model.Private_Key_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.Private_Key, storeScope);
                model.IvItsn_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.IvItsn, storeScope);
                model.IvAuth_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.IvAuth, storeScope);
                model.RON_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.RON, storeScope);
                model.EUR_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.EUR, storeScope);
                model.USD_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.USD, storeScope);
                model.OtherCurrencyId_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.Curency, storeScope);
                model.Relay_Response_URL_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.Relay_Response_URL, storeScope);
                model.RelayMethodId_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.RelayMethod, storeScope);
                model.TestMode_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.TestMode, storeScope);
                model.SSL_OverrideForStore = await _settingService.SettingExistsAsync(platiOnlinePaymentSettings, x => x.SSL, storeScope);
            }
          

            return View("~/Plugins/Payments.PlatiOnline/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var platiOnlinePaymentSettings = await _settingService.LoadSettingAsync<PlatiOnlinePaymentSettings>(storeScope);

            //save settings
            platiOnlinePaymentSettings.Merchant_Id = model.Merchant_Id;
            platiOnlinePaymentSettings.Public_Key = model.Public_Key;
            platiOnlinePaymentSettings.Private_Key = model.Private_Key;
            platiOnlinePaymentSettings.RON = model.RON;
            platiOnlinePaymentSettings.EUR = model.EUR;
            platiOnlinePaymentSettings.USD = model.USD;
            platiOnlinePaymentSettings.Curency = (Currency)model.OtherCurrencyId;
            platiOnlinePaymentSettings.Relay_Response_URL = model.Relay_Response_URL;
            platiOnlinePaymentSettings.RelayMethod = (RelayMethod)model.RelayMethodId;
            platiOnlinePaymentSettings.IvAuth = model.IvAuth;
            platiOnlinePaymentSettings.IvItsn = model.IvItsn;
            platiOnlinePaymentSettings.TestMode = model.TestMode;
            platiOnlinePaymentSettings.SSL = model.SSL;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.Merchant_Id, model.Merchant_Id_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.Public_Key, model.Public_Key_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.Private_Key, model.Private_Key_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.IvItsn, model.IvItsn_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.IvAuth, model.IvAuth_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.RON, model.RON_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.EUR, model.EUR_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.USD, model.USD_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.Curency, model.OtherCurrencyId_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.Relay_Response_URL, model.Relay_Response_URL_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.RelayMethod, model.RelayMethodId_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.TestMode, model.TestMode_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(platiOnlinePaymentSettings, x => x.SSL, model.SSL_OverrideForStore, storeScope, false);

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        public async Task<IActionResult> CancelOrder()
        {
            var order = (await _orderService.SearchOrdersAsync((await _storeContext.GetCurrentStoreAsync()).Id,
                customerId: (await _workContext.GetCurrentCustomerAsync()).Id, pageSize: 1)).FirstOrDefault();

            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("Homepage");
        }

        //[HttpPost]
        public async Task<IActionResult> CheckoutCompletedAsync()
        {
            /*if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();*/

            int orderId = Convert.ToInt32(_webHelper.QueryString<string>("orderId"));
            string error = _webHelper.QueryString<string>("error");

            CheckoutCompletedModel model = new CheckoutCompletedModel();

            OrderNote orderNote = new OrderNote();
            orderNote.CreatedOnUtc = DateTime.Now;
            orderNote.DisplayToCustomer = false;

            Order order = new Order();
            String note = "";

            if (error == null)
            {
                Po.Po po = new Po.Po();

                #region Merchant settings

                po.merchant_f_login = _platiOnlinePaymentSettings.Merchant_Id;
                po.merchant_ivItsn = _platiOnlinePaymentSettings.IvItsn;
                po.merchant_privateKey = _platiOnlinePaymentSettings.Private_Key;
                po.merchant_relay_response_f_relay_response_url = _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL) + _platiOnlinePaymentSettings.Relay_Response_URL;

                #endregion

                try
                {
                    switch (_platiOnlinePaymentSettings.RelayMethod.ToString())
                    {
                        #region 0.PTOR
                        case "PTOR":  //POST using JavaScript

                            string f_relay_message0 = Request.Form["f_relay_message"];
                            string f_crypt_message0 = Request.Form["f_crypt_message"];

                            po_auth_response response0 = (po_auth_response)po.Authorization.Response(f_relay_message0, f_crypt_message0);

                            order = await _orderService.GetOrderByIdAsync(Convert.ToInt32(response0.f_order_number));

                            //order_status
                            switch (response0.x_response_code)
                            {
                                case "1":
                                    order.PaymentStatusId = (int)PaymentStatus.PendingAuthorized;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                                case "2": 
                                    order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                    order.OrderStatusId = (int)OrderStatus.Processing;
                                    break;
                                case "3": 
                                    order.PaymentStatusId = (int)PaymentStatus.PendingSettleed;
                                    break;
                                case "5": 
                                    order.PaymentStatusId = (int)PaymentStatus.Settled;
                                    break;
                                case "6": 
                                    order.PaymentStatusId = (int)PaymentStatus.PendingVoided;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                                case "7": 
                                    order.PaymentStatusId = (int)PaymentStatus.Voided;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "8":
                                    order.PaymentStatusId = (int)PaymentStatus.Declined;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "9":
                                    order.PaymentStatusId = (int)PaymentStatus.Expired;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "10":
                                    order.PaymentStatusId = (int)PaymentStatus.Error;
                                    break;
                                case "13":
                                    order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                            }

                            //order_note
                            if (response0.x_response_code != "10")
                            {
                                note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                            }
                            else
                            {
                                note = "An error was encountered in PlatiOnline authorization process: " + response0.x_response_reason_text;
                            }

                            //order note
                            await _orderService.InsertOrderNoteAsync(new OrderNote
                            {
                                OrderId = order.Id,
                                Note = note,
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });

                            //update orer status
                            await _orderService.UpdateOrderAsync(order);

                            //model
                            model.order_number = response0.f_order_number;
                            model.order_status = Enum.GetName(typeof(OrderStatus), order.OrderStatusId);
                            model.payment_status = Enum.GetName(typeof(PaymentStatus), order.PaymentStatusId);
                            model.response_reason_text = response0.x_response_reason_text;

                            return View("Plugins/Payments.PlatiOnline/Views/CheckoutCompleted.cshtml", model);

                        #endregion

                        #region 1.POST_S2S_PO_PAGE
                        case "POST_S2S_PO_PAGE": //POST server PO to merchant server, customer get the PO template
                            
                            string f_relay_message1 = Request.Form["f_relay_message"];
                            string f_crypt_message1 = Request.Form["f_crypt_message"];

                            po_auth_response response1 = (po_auth_response)po.Authorization.Response(f_relay_message1, f_crypt_message1);

                            order = await _orderService.GetOrderByIdAsync(Convert.ToInt32(response1.f_order_number));

                            bool raspuns_procesat1 = true;

                            switch (response1.x_response_code)
                            {
                                case "1":
                                    order.PaymentStatusId = (int)PaymentStatus.PendingAuthorized;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                                case "2":
                                    order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                    order.OrderStatusId = (int)OrderStatus.Processing;
                                    break;
                                case "3":
                                    order.PaymentStatusId = (int)PaymentStatus.PendingSettleed;
                                    break;
                                case "5":
                                    order.PaymentStatusId = (int)PaymentStatus.Settled;
                                    break;
                                case "6":
                                    order.PaymentStatusId = (int)PaymentStatus.PendingVoided;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                                case "7":
                                    order.PaymentStatusId = (int)PaymentStatus.Voided;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "8":
                                    order.PaymentStatusId = (int)PaymentStatus.Declined;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "9":
                                    order.PaymentStatusId = (int)PaymentStatus.Expired;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "10":
                                    order.PaymentStatusId = (int)PaymentStatus.Error;
                                    break;
                                case "13":
                                    order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                            }

                            //order_note
                            if (response1.x_response_code != "10")
                            {
                                note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                            }
                            else
                            {
                                note = "An error was encountered in PlatiOnline authorization process: " + response1.x_response_reason_text;
                            }

                            //order note
                            await _orderService.InsertOrderNoteAsync(new OrderNote
                            {
                                OrderId = order.Id,
                                Note = note,
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });

                            // this works for f_relay_handshake = 1 in authorization request. I want HANDSHAKE between merchant server and PO server for POST_S2S_PO_PAGE
                            // if the response was processed, I send TRUE to PO server for PO_Transaction_Response_Processing
                            // if the response was not processed and I want the PO server to resend the transaction status, I send RETRY to PO server for PO_Transaction_Response_Processing
                            if (po.Authorization.transaction_relay_response.f_relay_handshake == "1")
                            {
                                //_webHelper.AppendHeaderAsync("User-Agent", "Mozilla/5.0 (Plati Online Relay Response Service)");
                                
                                if (raspuns_procesat1)
                                {
                                  // Response.AppendHeader("PO_Transaction_Response_Processing", "true");
                                }
                                else
                                {
                                    //Response.AppendHeader("PO_Transaction_Response_Processing", "retry");
                                }
                            }
                            return new EmptyResult();
                        #endregion

                        #region 2.POST_S2S_MT_PAGE
                        case "POST_S2S_MT_PAGE": //POST server PO to merchant server, customer get the Merchant template

                            string f_relay_message2 = Request.Form["f_relay_message"];
                            string f_crypt_message2 = Request.Form["f_crypt_message"];

                            po_auth_response response2 = (po_auth_response)po.Authorization.Response(f_relay_message2, f_crypt_message2);

                            order = await _orderService.GetOrderByIdAsync(Convert.ToInt32(response2.f_order_number));

                            bool raspuns_procesat2 = true;

                            switch (response2.x_response_code)
                            {
                                case "1":
                                    order.PaymentStatusId = (int)PaymentStatus.PendingAuthorized;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                                case "2":
                                    order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                    order.OrderStatusId = (int)OrderStatus.Processing;
                                    break;
                                case "3":
                                    order.PaymentStatusId = (int)PaymentStatus.PendingSettleed;
                                    break;
                                case "5":
                                    order.PaymentStatusId = (int)PaymentStatus.Settled;
                                    break;
                                case "6":
                                    order.PaymentStatusId = (int)PaymentStatus.PendingVoided;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                                case "7":
                                    order.PaymentStatusId = (int)PaymentStatus.Voided;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "8":
                                    order.PaymentStatusId = (int)PaymentStatus.Declined;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "9":
                                    order.PaymentStatusId = (int)PaymentStatus.Expired;
                                    order.OrderStatusId = (int)OrderStatus.Cancelled;
                                    break;
                                case "10":
                                    order.PaymentStatusId = (int)PaymentStatus.Error;
                                    break;
                                case "13":
                                    order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                    order.OrderStatusId = (int)OrderStatus.Pending;
                                    break;
                            }

                            //order_note
                            if (response2.x_response_code != "10")
                            {
                                note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                            }
                            else
                            {
                                note = "An error was encountered in PlatiOnline authorization process: " + response2.x_response_reason_text;
                            }

                            //order note
                            await _orderService.InsertOrderNoteAsync(new OrderNote
                            {
                                OrderId = order.Id,
                                Note = note,
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });

                            // instead of sending a <h2> tag using echo, you can send an HTML code, based on X_RESPONSE_CODE
                            // this works for f_relay_handshake = 1 in authorization request. I want HANDSHAKE between merchant server and PO server for POST_S2S_MT_PAGE
                            // if the response was processed, I send TRUE to PO server for PO_Transaction_Response_Processing
                            // if the response was not processed and I want the PO server to resend the transaction status, I send RETRY to PO server for PO_Transaction_Response_Processing
                            if (po.Authorization.transaction_relay_response.f_relay_handshake == "1")
                            {
                                //Response.AppendHeader("User-Agent", "Mozilla/5.0 (Plati Online Relay Response Service)");

                                if (raspuns_procesat2)
                                {
                                    //Response.AppendHeader("PO_Transaction_Response_Processing", "true");
                                }
                                else
                                {
                                    //Response.AppendHeader("PO_Transaction_Response_Processing", "retry");
                                }
                            }

                            //model
                            model.order_number = response2.f_order_number;
                            model.order_status = Enum.GetName(typeof(OrderStatus), order.OrderStatusId);
                            model.payment_status = Enum.GetName(typeof(PaymentStatus), order.PaymentStatusId);
                            model.response_reason_text = response2.x_response_reason_text;
                            
                            return View("Plugins/Payments.PlatiOnline/Views/CheckoutCompleted.cshtml", model);

                        #endregion
                    }
                }
                catch (Exception e)
                {
                    model.order_number = orderId.ToString();
                    model.order_status = OrderStatus.Pending.ToString();
                    model.payment_status = PaymentStatus.Error.ToString();
                    model.response_reason_text = "An error was encountered in PlatiOnline authorization process: " + HttpUtility.UrlDecode(e.Message);
                }
            }
            else
            {
                //order
                order = await _orderService.GetOrderByIdAsync(orderId);
                order.PaymentStatusId = (int)PaymentStatus.Error;
                order.OrderStatusId = (int)OrderStatus.Pending;
                await _orderService.UpdateOrderAsync (order);

                //order note
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = "An error was encountered in PlatiOnline authorization process: " + HttpUtility.UrlDecode(error),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                model.order_number = orderId.ToString();
                model.order_status = Enum.GetName(typeof(OrderStatus), order.OrderStatusId);
                model.payment_status = Enum.GetName(typeof(PaymentStatus), order.PaymentStatusId);
                model.response_reason_text = error;
            }

            return View("~/Plugins/Payments.PlatiOnline/Views/CheckoutCompleted.cshtml", model);
        }

        [HttpPost]
        public async Task<ContentResult> ITSN()
        {
            Po.Po po = new Po.Po();
           
            #region Merchant settings

            po.merchant_f_login = _platiOnlinePaymentSettings.Merchant_Id;
            po.merchant_ivAuth = _platiOnlinePaymentSettings.IvAuth;
            po.merchant_ivItsn = _platiOnlinePaymentSettings.IvItsn;
            po.merchant_privateKey = _platiOnlinePaymentSettings.Private_Key;
            po.merchant_publicKey = _platiOnlinePaymentSettings.Public_Key;
            po.merchant_relay_response_f_relay_response_url = _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL) + _platiOnlinePaymentSettings.Relay_Response_URL;
            //po.log_path = @"";

            #endregion

            #region get_itsn_request

            string f_relay_message = Request.Form["f_itsn_message"];
            string f_crypt_message = HttpUtility.UrlDecode(Request.Form["f_crypt_message"]).Replace(" ","+");

            #endregion

            try
            {
                #region process_itsn_request

                po_itsn po_itsn = (po_itsn)po.Itsn.Response(f_relay_message, f_crypt_message);

                #endregion

                #region set_query_fields(for itsn)

                po.Query.f_order_number = po_itsn.f_order_number;
                po.Query.x_trans_id = po_itsn.x_trans_id;

                #endregion

                #region query_request(for itsn)

                po_query_response po_query_response = po.Query.Request<po_query_response>();

                #endregion

                #region process_query_response(for itsn)

                if (!po.Query.HasError)
                {
                    if (po_query_response.po_error_code == "0")
                    {
                        #region Update order status

                        var order = await _orderService.GetOrderByIdAsync(Convert.ToInt32(po_query_response.order.f_order_number));

                        //order note
                        await _orderService.InsertOrderNoteAsync(new OrderNote
                        {
                            OrderId = order.Id,
                            Note = "[ITSN] Notification: transaction status was changed!",
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        string f_response_code = "1";

                        switch (po_query_response.order.tranzaction.status_fin1.code)
                        {
                            case "1":
                                order.PaymentStatusId = (int)PaymentStatus.PendingAuthorized;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            case "2":
                                order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                order.OrderStatusId = (int)OrderStatus.Processing;
                                break;
                            case "3":
                                order.PaymentStatusId = (int)PaymentStatus.PendingSettleed;
                                //order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            case "5":
                                switch (po_query_response.order.tranzaction.status_fin2.code)
                                {
                                    case "1":
                                        order.PaymentStatusId = (int)PaymentStatus.Pending;
                                        order.OrderStatusId = (int)OrderStatus.Pending;
                                        break;
                                    case "2":
                                        order.PaymentStatusId = (int)PaymentStatus.Refunded;
                                        order.OrderStatusId = (int)OrderStatus.Cancelled;
                                        break;
                                    case "3":
                                        order.PaymentStatusId = (int)PaymentStatus.Refused;
                                        order.OrderStatusId = (int)OrderStatus.Cancelled;
                                        break;
                                    case "4":
                                        order.PaymentStatusId = (int)PaymentStatus.Settled;
                                        //order.OrderStatusId = (int)OrderStatus.Settled;
                                        break;
                                }
                                break;
                            case "6":
                                order.PaymentStatusId = (int)PaymentStatus.PendingVoided;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            case "7":
                                order.PaymentStatusId = (int)PaymentStatus.Voided;
                                order.OrderStatusId = (int)OrderStatus.Cancelled;
                                break;
                            case "8":
                                order.PaymentStatusId = (int)PaymentStatus.Declined;
                                order.OrderStatusId = (int)OrderStatus.Cancelled;
                                break;
                            case "9":
                                order.PaymentStatusId = (int)PaymentStatus.Expired;
                                order.OrderStatusId = (int)OrderStatus.Cancelled;
                                break;
                            case "10":
                                order.PaymentStatusId = (int)PaymentStatus.Error;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            case "13":
                                order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            default:
                                f_response_code = "0";
                                break;
                        }

                        //order note
                        await _orderService.InsertOrderNoteAsync(new OrderNote
                        {
                            OrderId = order.Id,
                            Note = "[ITSN] PlatiOnline transaction status : " + order.PaymentStatus,
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        //update orer status
                        await _orderService.UpdateOrderAsync(order);

                        #endregion

                        #region Send ITSN response

                        XmlDocument doc = po.Itsn.ItsnResponse(f_response_code, po_query_response.order.tranzaction.x_trans_id);

                        _contentResult = doc.OuterXml;

                        #endregion
                    }
                    else//1 - an error occurred parsing the '''Query Request XML_Message''' and PlatiOnline will not process the request;
                    {
                        _contentResult = po_query_response.po_error_reason;
                    }
                }
                else
                {
                    _contentResult = po.Query.GetError().Error;
                }

                #endregion
            }
            catch (Exception e)
            {
                _contentResult = e.Message;
            }

            return Content(_contentResult);
        }
  
        #endregion
    }
}