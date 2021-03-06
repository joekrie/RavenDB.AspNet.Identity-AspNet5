﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using RavenDB.AspNet.Identity;
using Xunit;
using Xunit.Extensions;
using Microsoft.Framework.DependencyInjection;

namespace RavenDB.AspNet.Identity.Tests
{
    public class LoginStore : BaseTest
    {
        [Fact]
        public async void Can_create_user_and_log_in()
        {
            const string username = "DavidBoike";
            const string userId = "user_id_1";
            string password = Guid.NewGuid().ToString("n");
            const string googleLogin = "http://www.google.com/fake/user/identifier";
            const string yahooLogin = "http://www.yahoo.com/fake/user/identifier";

            var user = new SimpleAppUser { Id = userId, UserName = username };

            var services = new ServiceCollection()
                .AddTransient<IUserStore<SimpleAppUser>, UserStore<SimpleAppUser>>()
                .AddIdentity<SimpleAppUser, >

            using (var docStore = NewDocStore())
            {
                using (var session = docStore.OpenAsyncSession())
                {
                    using (var mgr = new UserManager<SimpleAppUser>(new UserStore<SimpleAppUser>(session), ))
                    {
                        IdentityResult result = await mgr.CreateAsync(user, password);

                        Assert.True(result.Succeeded);
                        Assert.NotNull(user.Id);

                        var res1 = await mgr.AddLoginAsync(user, new UserLoginInfo("Google", googleLogin, "GoogleDisplayName"));
                        var res2 = await mgr.AddLoginAsync(user, new UserLoginInfo("Yahoo", yahooLogin, "YahooDisplayName"));

                        Assert.True(res1.Succeeded);
                        Assert.True(res2.Succeeded);
                    }
                    await session.SaveChangesAsync();
                }

                using (var session = docStore.OpenSession())
                {
                    var loaded = session.Load<SimpleAppUser>(user.Id);
                    Assert.NotNull(loaded);
                    Assert.NotSame(loaded, user);
                    Assert.Equal(loaded.Id, user.Id);
                    Assert.Equal(loaded.UserName, user.UserName);
                    Assert.NotNull(loaded.PasswordHash);

                    Assert.Equal(loaded.Logins.Count, 2);
                    Assert.True(loaded.Logins.Any(x => x.LoginProvider == "Google" && x.ProviderKey == googleLogin));
                    Assert.True(loaded.Logins.Any(x => x.LoginProvider == "Yahoo" && x.ProviderKey == yahooLogin));

                    var loadedLogins = session.Advanced.LoadStartingWith<IdentityUserLogin>("IdentityUserLogins/");
                    Assert.Equal(loadedLogins.Length, 2);

                    foreach (var login in loaded.Logins)
                    {
                        var loginDoc = session.Load<IdentityUserLogin>(Util.GetLoginId(login));
                        Assert.Equal(login.LoginProvider, loginDoc.Provider);
                        Assert.Equal(login.ProviderKey, loginDoc.ProviderKey);
                        Assert.Equal(user.Id, loginDoc.UserId);
                    }
                }

                using (var session = docStore.OpenSession())
                {
                    using (var mgr = new UserManager<SimpleAppUser>(new UserStore<SimpleAppUser>(session)))
                    {
                        var userByName = await mgr.FindByNameAsync(username);
                        var userByGoogle = await mgr.FindByLoginAsync("Google", googleLogin);
                        var userByYahoo = await mgr.FindByLoginAsync("Yahoo", yahooLogin);

                        Assert.NotNull(userByName);
                        Assert.NotNull(userByGoogle);
                        Assert.NotNull(userByYahoo);

                        Assert.Equal(userByName.Id, userId);
                        Assert.Equal(userByName.UserName, username);

                        // The Session cache should return the very same objects
                        Assert.Same(userByName, userByGoogle);
                        Assert.Same(userByName, userByYahoo);
                    }
                    session.SaveChanges();
                }
            }
        }
    }
}
