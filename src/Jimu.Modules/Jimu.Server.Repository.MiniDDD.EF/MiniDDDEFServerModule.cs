﻿using Autofac;
using Jimu.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniDDD;
using MiniDDD.UnitOfWork;
using MiniDDD.UnitOfWork.EF;
using System;
using System.Linq;

namespace Jimu.Server.Repository.MiniDDD.EF
{
    public class MiniDDDEFServerModule : ServerModuleBase
    {
        readonly MiniDDDEFOptions _options;
        IContainer _container = null;
        public MiniDDDEFServerModule(IConfigurationRoot jimuAppSettings) : base(jimuAppSettings)
        {
            _options = jimuAppSettings.GetSection(typeof(MiniDDDEFOptions).Name).Get<MiniDDDEFOptions>();
        }

        public override void DoServiceRegister(ContainerBuilder serviceContainerBuilder)
        {
            if (_options != null && _options.Enable)
            {
                DbContextOptions dbContextOptions = new DbContextOptions
                {
                    ConnectionString = _options.ConnectionString,
                    DbType = _options.DbType
                };
                Action<LogLevel, string> logAction = null;
                if (_options.OpenLogTrace)
                {
                    logAction = (level, log) =>
                    {
                        if (_container != null && _container.IsRegistered<Jimu.Logger.ILogger>())
                        {
                            Jimu.Logger.ILogger logger = _container.Resolve<Jimu.Logger.ILogger>();
                            logger.Info($"【EF】 - LogLevel: {level} - {log}");
                        }
                    };
                }

                DefaultDbContext dbContext = new DefaultDbContext(dbContextOptions, _options.TableModelAssemblyName, logAction, _options.LogLevel);
                serviceContainerBuilder.RegisterType<UnitOfWork>()
                    .WithParameter("context", dbContext)
                    .AsImplementedInterfaces()
                    .InstancePerLifetimeScope();

                // register repository
                var repositoryType = typeof(InlineEventHandler);
                var assembies = AppDomain.CurrentDomain.GetAssemblies();
                var repositories = assembies.SelectMany(x => x.GetTypes()).Where(x =>
                {
                    if (x.IsClass && !x.IsAbstract && typeof(InlineEventHandler).IsAssignableFrom(x))
                    {
                        foreach (var face in x.GetInterfaces())
                        {
                            var isRepository = face.IsGenericType && face.GetGenericTypeDefinition() == typeof(IRepository<,>);
                            if (isRepository) return true;
                        }
                    }
                    return false;
                }).ToList();
                repositories.ForEach(x => serviceContainerBuilder.RegisterType(x).AsImplementedInterfaces().InstancePerLifetimeScope());
            }
            base.DoServiceRegister(serviceContainerBuilder);
        }

        public override void DoInit(IContainer container)
        {
            _container = container;
            base.DoServiceInit(container);
        }
    }
}
