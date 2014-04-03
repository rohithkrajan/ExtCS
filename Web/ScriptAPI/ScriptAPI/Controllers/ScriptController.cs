using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using ScriptAPI.Models;

namespace ScriptAPI.Controllers
{
    public class ScriptController : ApiController
    {
        private ScriptDBContext db = new ScriptDBContext();

        // GET api/Script
        public IEnumerable<Script> GetScripts()
        {
            return db.Scripts.AsEnumerable();
        }

        // GET api/Script/5
        public Script GetScript(int id)
        {
            int SID = id;
            Script script = db.Scripts.Find(SID);
            if (script == null)
            {
                throw new HttpResponseException(Request.CreateResponse(HttpStatusCode.NotFound));
            }

            return script;
        }

        // PUT api/Script/5
        public HttpResponseMessage PutScript(int id, Script script)
        {
            if (ModelState.IsValid && id == script.SID)
            {
                db.Entry(script).State = EntityState.Modified;

                try
                {
                    db.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }

                return Request.CreateResponse(HttpStatusCode.OK);
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        // Get api/Script?sname="value"
        public IEnumerable<Script> GetScriptbyname(string sname)
        {
            var scripts = from m in db.Scripts select m;

            if (!String.IsNullOrEmpty(sname))
            {
                scripts = scripts.Where(s => s.SName.Contains(sname));
            }

            return scripts.AsEnumerable();

        }

        // POST api/Script
        public HttpResponseMessage PostScript(Script script)
        {
            if (ModelState.IsValid)
            {
                db.Scripts.Add(script);
                db.SaveChanges();

                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Created, script);
                response.Headers.Location = new Uri(Url.Link("DefaultApi", new { id = script.SID }));
                return response;
            }
            else
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }
        }

        // DELETE api/Script/5
        public HttpResponseMessage DeleteScript(int id)
        {
            Script script = db.Scripts.Find(id);
            if (script == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            db.Scripts.Remove(script);

            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            return Request.CreateResponse(HttpStatusCode.OK, script);
        }

        protected override void Dispose(bool disposing)
        {
            db.Dispose();
            base.Dispose(disposing);
        }
    }
}