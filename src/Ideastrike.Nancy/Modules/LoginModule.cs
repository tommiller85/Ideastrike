﻿using System;
using System.Configuration;
using System.Linq;
using Ideastrike.Nancy.Localization;
using Ideastrike.Nancy.Models;
using Nancy;
using System.Net;
using Newtonsoft.Json;

namespace Ideastrike.Nancy.Modules
{
    public class LoginModule : NancyModule
    {
        public string Apikey;
        private IUserRepository _user;

        public LoginModule(IUserRepository userRepository)
        {
            _user = userRepository;
            Apikey = ConfigurationManager.AppSettings["JanrainKey"];

            Post["/login/token"] = x =>
            {
                if (string.IsNullOrWhiteSpace(Request.Form.token))
                    return
                        View["Login/Error",
                            new
                                {
                                    Title = Strings.LoginModule_LoginError,
                                    Message = Strings.LoginModule_BadResponse_NoToken
                                }];

                var response = new WebClient().DownloadString(string.Format("https://rpxnow.com/api/v2/auth_info?apiKey={0}&token={1}",Apikey, Request.Form.token));

                if (string.IsNullOrWhiteSpace(response))
                    return
                        View["Login/Error",
                            new
                                {
                                    Title = Strings.LoginModule_LoginError,
                                    Message = Strings.LoginModule_BadResponse_NoUser
                                }];

                var j = JsonConvert.DeserializeObject<dynamic>(response);

                if (j.stat.ToString() != "ok")
                    return
                        View["Login/Error",
                            new
                                {
                                    Title = Strings.LoginModule_LoginError,
                                    Message = Strings.LoginModule_BadResponse
                                }];

                var userIdentity = j.profile.identifier.ToString();
                var username = j.profile.preferredUsername.ToString();
                string email = string.Empty;
                if (j.profile.email != null)
                    email = j.profile.email.ToString();
                var user = _user.GetUserFromUserIdentity(userIdentity);

                if (user == null)
                {
                    var u = new User
                                {
                                    Id = Guid.NewGuid(),
                                    Identity = userIdentity,
                                    UserName = (!string.IsNullOrEmpty(username)) ? username : "New User " + _user.GetAll().Count(),
                                    Email = (!string.IsNullOrEmpty(email)) ? email : "none@void.com",
                                    Github = (!string.IsNullOrEmpty(username)) ? username : "",
                                    IsActive = true,
                                };

                    if (!_user.GetAll().Any())
                        _user.AddRole(u, "Admin");

                    if (j.profile.photo != null)
                        u.AvatarUrl = j.profile.photo.ToString();

                    _user.Add(u);
                    return this.LoginAndRedirect(u.Id, DateTime.Now.AddDays(1), "/profile/edit");
                }

                return ModuleExtensions.Login(this, user.Id, DateTime.Now.AddDays(1), "/");
            };

            Get["/logout/"] = parameters => this.LogoutAndRedirect("/");
        }
    }
}