using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contracts.Auth;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Models.Auth;
using Models.UserTypes;

namespace TaxiAuth
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class TaxiAuth : StatelessService, IAuthLogic
    {
        private readonly Contracts.Database.IData dbService;

        public TaxiAuth(StatelessServiceContext context, Contracts.Database.IData dbService)
            : base(context)
        {
            this.dbService = dbService;
        }

        #region AuthMethods

        public async Task<UserProfile> GetUserProfile(Guid id)
        {
            return await dbService.GetUserProfile(id);
        }

        public async Task<LoginResponse> Login(LoginData loginData)
        {
            UserProfile existingUser = null;
            foreach (UserType type in Enum.GetValues(typeof(UserType)))
            {
                if (loginData.authType == AuthType.TRADITIONAL)
                {
                    existingUser = await dbService.ExistsWithPwd(loginData.Email, loginData.Password);
                }
                // Google Auth
                else
                {
                    existingUser = await dbService.ExistsOnlyEmail(loginData.Email);
                }

                if (existingUser != null)
                {
                    var res = new LoginResponse()
                    {
                        userId = (Guid)existingUser.Id,
                        userType = existingUser.Type
                    };
                    if (existingUser.Type == UserType.ADMIN)
                    {
                        res.roleId = ((Admin)existingUser).AdminId;
                    }
                    else if (existingUser.Type == UserType.CLIENT)
                    {
                        res.roleId = ((Client)existingUser).ClientId;
                    }
                    else if (existingUser.Type == UserType.DRIVER)
                    {
                        res.roleId = ((Driver)existingUser).DriverId;
                    }
                    return res;
                }
            }

            return null;
        }

        public async Task<bool> Register(UserProfile userProfile)
        {
            var userExists = false;
            foreach (UserType type in Enum.GetValues(typeof(UserType)))
            {
                userExists |= await dbService.ExistsOnlyEmail(userProfile.Email) != null;
            }

            if (userExists)
            {
                return false;
            }

            if (userProfile.Type == UserType.DRIVER)
            {
                var newDriver = new Models.UserTypes.Driver(userProfile, Models.UserTypes.DriverStatus.NOT_VERIFIED);
                newDriver.DriverId = Guid.NewGuid();
                return await dbService.CreateDriver(newDriver);
            }
            else if (userProfile.Type == UserType.CLIENT)
            {
                var newClient = new Models.UserTypes.Client(userProfile);
                newClient.ClientId = Guid.NewGuid();
                return await dbService.CreateClient(newClient);
            }

            return await dbService.CreateUser(userProfile);
        }

        public async Task<UserProfile> UpdateUserProfile(UpdateUserProfileRequest updateUserProfileRequest, Guid id)
        {
            return await dbService.UpdateUserProfile(updateUserProfileRequest, id);
        }

        #endregion

        #region ServiceFabricMethods
        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }


        #endregion

    }
}
