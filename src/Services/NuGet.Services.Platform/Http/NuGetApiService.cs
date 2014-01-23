﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Web.Http.Routing;
using Autofac;
using Autofac.Integration.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet.Services.Client;
using NuGet.Services.Composition;
using NuGet.Services.Http.Authentication;
using NuGet.Services.Http.Controllers;
using NuGet.Services.Http.Models;
using NuGet.Services.ServiceModel;
using Owin;

namespace NuGet.Services.Http
{
    public abstract class NuGetApiService : NuGetHttpService
    {
        public NuGetApiService(ServiceHost host) : base(host) { }

        protected override void Configure(IAppBuilder app)
        {
            var config = Container.Resolve<HttpConfiguration>();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(Container);
            if (!String.IsNullOrEmpty(Configuration.Http.AdminKey))
            {
                app.UseAdminKeyAuthentication(new AdminKeyAuthenticationOptions()
                {
                    Key = Configuration.Http.AdminKey,
                    GrantedRole = Roles.Admin
                });
            }
            app.UseWebApi(config);
        }

        public override void RegisterComponents(Autofac.ContainerBuilder builder)
        {
            base.RegisterComponents(builder);
            
            builder.RegisterInstance(this).As<NuGetApiService>();

            var config = ConfigureWebApi();
            builder.RegisterInstance(config).AsSelf();

            builder
                .RegisterApiControllers(GetControllerAssemblies().ToArray())
                .OnActivated(e =>
                {
                    var nugetController = e.Instance as NuGetApiController;
                    if (nugetController != null)
                    {
                        nugetController.Host = e.Context.Resolve<ServiceHost>();
                        nugetController.Service = e.Context.Resolve<NuGetApiService>();
                        nugetController.Container = e.Context.Resolve<IComponentContainer>();
                    }
                })
                .InstancePerApiRequest();

            //builder.RegisterWebApiFilterProvider(config);
            //builder.RegisterWebApiModelBinderProvider();
        }

        protected virtual IEnumerable<Assembly> GetControllerAssemblies()
        {
            yield return typeof(NuGetApiService).Assembly;
            
            if (GetType().Assembly != typeof(NuGetApiService).Assembly)
            {
                yield return GetType().Assembly;
            }
        }

        protected virtual HttpConfiguration ConfigureWebApi()
        {
            var config = new HttpConfiguration();

            config.Formatters.Clear();
            config.Formatters.Add(JsonFormat.Formatter);

            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
            config.Filters.Add(new ApiExceptionFilter());

            // Use Attribute routing
            var resolver = new DefaultInlineConstraintResolver();
            ConfigureAttributeRouting(resolver);
            config.MapHttpAttributeRoutes(resolver);

#if DEBUG
            config.Services.Replace(
                typeof(IHttpActionInvoker),
                new DebugActionInvoker(config.Services.GetActionInvoker()));

            config.Services.Replace(
                typeof(IHttpActionSelector),
                new DebugActionSelector(config.Services.GetActionSelector()));

            config.Services.Replace(
                typeof(IHttpControllerSelector),
                new DebugControllerSelector(config.Services.GetHttpControllerSelector()));
#endif

            return config;
        }

        protected virtual void ConfigureAttributeRouting(DefaultInlineConstraintResolver resolver)
        {
        }

        public abstract Task<object> GetApiModel(NuGetApiController controller, IPrincipal requestor);

#if DEBUG
        // Debug services so we can step in to them.
        private class DebugActionInvoker : IHttpActionInvoker
        {
            private IHttpActionInvoker httpActionInvoker;

            public DebugActionInvoker(IHttpActionInvoker httpActionInvoker)
            {
                // TODO: Complete member initialization
                this.httpActionInvoker = httpActionInvoker;
            }


            public Task<System.Net.Http.HttpResponseMessage> InvokeActionAsync(HttpActionContext actionContext, System.Threading.CancellationToken cancellationToken)
            {
                return httpActionInvoker.InvokeActionAsync(actionContext, cancellationToken);
            }
        }

        private class DebugActionSelector : IHttpActionSelector
        {
            private IHttpActionSelector httpActionSelector;

            public DebugActionSelector(IHttpActionSelector httpActionSelector)
            {
                // TODO: Complete member initialization
                this.httpActionSelector = httpActionSelector;
            }

            public ILookup<string, HttpActionDescriptor> GetActionMapping(HttpControllerDescriptor controllerDescriptor)
            {
                return httpActionSelector.GetActionMapping(controllerDescriptor);
            }

            public HttpActionDescriptor SelectAction(HttpControllerContext controllerContext)
            {
                return httpActionSelector.SelectAction(controllerContext);
            }
        }

        private class DebugControllerSelector : IHttpControllerSelector
        {
            private IHttpControllerSelector httpControllerSelector;

            public DebugControllerSelector(IHttpControllerSelector httpControllerSelector)
            {
                // TODO: Complete member initialization
                this.httpControllerSelector = httpControllerSelector;
            }
            public IDictionary<string, HttpControllerDescriptor> GetControllerMapping()
            {
                return httpControllerSelector.GetControllerMapping();
            }

            public HttpControllerDescriptor SelectController(System.Net.Http.HttpRequestMessage request)
            {
                return httpControllerSelector.SelectController(request);
            }
        }
#endif
    }
}
