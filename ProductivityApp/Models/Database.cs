﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProductivityApp.Controllers;

namespace ProductivityApp.Models
{
    /// <summary>
    /// The Database implements the dbcontext functionality and exposes specific ease-of-use functions for manipulating flows
    /// </summary>
    public class Database : DbContext, IDatabase
    {

        /// <summary>
        /// The list of flows (user instances of templates)
        /// </summary>
        private DbSet<Flow> Flows { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=flows.db");
            
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);  
            
            
        }


        /// <summary>
        /// Instantiate a new flow object from the source template.
        /// The new flow object is then saved to a new id, and returned as the function output
        /// </summary>
        /// <param name="template">The source template to copy</param>
        /// <returns>the newly instantiated flow</returns>
        public Flow InitializeTemplate(Flow template,IFileHandler fileHandler)
        {
            //get a copy of flow from the template
            var newFlow = template.initializeFlow();

            //copy forms from template to newFlow
            fileHandler.InstantiateDirectory(template.Id,newFlow.Id);
            
            //add the new flow to the tracked database
            Flows.Add(newFlow);
            //commit changes, must do this!
            SaveChanges();
            return newFlow;
        }
        ///<summary>
        /// This method saves a flow to a dbset called Flows
        /// <param name="flow">The ViewModel returned from the Fill activity</param>
        /// <returns>flow</returns>
        ///</summary>
        public Flow SaveFlow(FlowController.FillViewModel flow)
        {

            var existingFlow = Flows
                .Include(f=>f.inputSurvey).ThenInclude(f=>f.fields)
                .Include(f=>f.forms).ThenInclude(f=>f.assignments).ThenInclude(f=>f.filter)
                .Include(f=>f.criteria)
                .Include(f=>f.destination).Where(f => f.Id == flow.Id).FirstOrDefault();

            if(existingFlow == null)
            {
                throw new ArgumentException("The specified flow does not exist.");
            }
            //fill in field answers frmo the user
            foreach(var field in existingFlow.inputSurvey.fields)
            {
                var userField = flow.inputSurvey.fields.Where(f => f.Id == field.Id).FirstOrDefault();                
                if(userField != null)
                {
                    field.answer = userField.answer;
                }
                else
                {
                    field.answer = "";
                }
            }
            //Fill in criteria from user input
            foreach(var criteria in existingFlow.criteria)
            {
                var userCriteria = flow.criteria.Where(c => c.Category == criteria.Category).FirstOrDefault();
                if(userCriteria != null)
                {
                    criteria.SelectedValue = userCriteria.SelectedValue;
                }
                else
                {
                    criteria.SelectedValue = null;
                }
            }
            if (flow.destination != null)
            {
                existingFlow.destination.EmailAddresses = flow.destination.EmailAddresses;
                existingFlow.destination.zip = flow.destination.zip;
            }
            
            SaveChanges();
            return existingFlow;
        }
        ///<summary>
         ///This method finds and removes a flow from the DBSet called Flows by identifying a specified GUID
        /// <param name="id">The Guid of the flow to be deleted</param>
        ///</summary>
        public void DeleteFlow(Guid Id)
        {
            var flow = Flows.Where(f => f.Id == Id).FirstOrDefault();
            //flow.criteria = new List<Criteria>();
            Flows.Remove(flow);
            SaveChanges();
            
        }
        ///<summary>
        /// This method gets a flow from the database by its Guid 
        /// <param name="id">The Guid of the desired flow</param>
        /// <returns>flow</returns>
        ///</summary>
        public Flow FindFlowById(Guid Id) {
            var flow = Flows.Where(t=> !t.IsATemplate && (t.Id == Id));
            return flow.Single();
        }
        /// <summary>
        /// Get all forms in the database that are flagged as a template
        /// </summary>
        /// <returns>Collection of forms</returns>
        public IList<Flow> GetForms()
        {
            //get all the forms that are not flagged as explicitly a template
            //and include ALL subfields that exist (well, honestly, ones that I remembered!) -mg
            var forms = Flows.Where(t => !t.IsATemplate)
            .Include(t => t.inputSurvey).ThenInclude(t => t.fields)
                .Include(t => t.criteria).ThenInclude(c => c.answers)
                .Include(t => t.destination)
                //.Include(t => t.assignments).ThenInclude(t => t.inputField)
                //.Include(t => t.assignments).ThenInclude(t => t.outputField)
                //.Include(t => t.assignments).ThenInclude(t => t.filter)
                .ToList();
            return forms;
        }
        
        ///<summary>
        /// This method gets the templates from the database
        /// NOTE: this is not being used yet, it redirects to the sample templates
        /// <returns>Collection of template flows</returns>
        ///</summary>
        public IList<Flow> GetTemplates()
        {
            return GetSampleTemplates();
            var templates = Flows.Where(t => t.IsATemplate);
            //get sample flow if none exist
            if(templates.Count() < 3)
            {
                foreach(var template in GetSampleTemplates())
                {
                    Flows.Add(template);
                }
                SaveChanges();
            }
            //This is setup so that I get all the sub-tables required. Sadly we need to do this in EF net core. You will have to do this in GetFlows() as well! -mg
            return Flows.Where(t=>t.IsATemplate).Include(t=>t.inputSurvey).ThenInclude(t=>t.fields)
                .Include(t=>t.criteria).ThenInclude(c=>c.answers)
                .Include(t=>t.destination)
                //.Include(t=>t.assignments).ThenInclude(t=>t.inputField)
                //.Include(t => t.assignments).ThenInclude(t => t.outputField)
                //.Include(t => t.assignments).ThenInclude(t => t.filter)
                .ToList();
        }

        ///<summary>
        /// This method gets the user created flows from the database
        /// <returns>Collection of flows</returns>
        ///</summary>
        public IList<Flow> GetFlows() {
            SaveChanges();
            var flows = Flows.Where(t=> !t.IsATemplate) ;
            return Flows.Where(t=>!t.IsATemplate).Include(t=>t.inputSurvey).ThenInclude(t=>t.fields)
                .Include(t=>t.forms).ThenInclude(f=>f.assignments).ThenInclude(f=>f.filter)
                .Include(t=>t.criteria).ThenInclude(c=>c.answers)
                .Include(t=>t.destination)
                .OrderByDescending(t=>t.inputSurvey.timeCreated)
                //.Include(t=>t.assignments).ThenInclude(t=>t.inputField)
                //.Include(t => t.assignments).ThenInclude(t => t.outputField)
                //.Include(t => t.assignments).ThenInclude(t => t.filter)
                .ToList();

        }
        ///<summary>
        /// This method gets the sample templates that flows are copied from
        /// <returns>Collection of template flows</returns>
        ///</summary>
        public List<Flow> GetSampleTemplates()
        {//make a sample flow
            Flow template1 = new Flow
            {
                IsATemplate = true,
                name = "Help Desk Questionaire",
                ThumbnailImage = "placeholder.jpg",
                Id = new Guid("5710c736-f5b9-475f-9ef5-76529ea11111"),
                Description = "Demo of the helpdesk questionaire",
                 forms = new List<Form> {
                    new Form {
                        name = "NESD_Questionnaire",
                        fileName = "NESD_Questionnaire.doc",
                        kind = "doc",
                        assignments = new List<Assignment> {
                         //put assignments here  
                        }   
                    }
                 },
                inputSurvey = new Survey
                {
                    Id = Guid.NewGuid(),
                    fields = new List<Field> {
                     new Field(Field.Kinds.String,"firstname","Please enter your first name",null),
                     new Field(Field.Kinds.String,"lastname","Please enter your last name",null),
                     new Field(Field.Kinds.String,"jobtitle","Please enter your job title",null),
 
                    }
                },
               // assignments = new List<Assignment>(),
                criteria = new List<Criteria> {
                  new Criteria{
                       Id = Guid.NewGuid(),
                       prompt = "Credit Card?",
                       Category = "card",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                           new Answer("No","no"),
                           new Answer("Unknown","unknown")

                       }

                  },
                  new Criteria{
                      Id = Guid.NewGuid(),
                       prompt = "Purchase less than $700,000?",
                       Category = "less700k",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                           new Answer("No","no"),
                           new Answer("Unknown","unknown")

                       }

                  },
                new Criteria{
                    Id = Guid.NewGuid(),
                   prompt = "Purchase between $700,000 and $13.5 Million?",
                   Category = "gr100",
                   answers = new List<Answer>
                   {
                       new Answer("Yes","yes"),
                       new Answer("No","no"),
                       new Answer("Unknown","unknown")

                    }
                  }
              },
                destination =  new Destination()
            };
            Flow template2 = new Flow
            {
                IsATemplate = true,
                ThumbnailImage = "placeholder.jpg",
                Id = Guid.NewGuid(),
                name = "Hire",
                Description = "Hire people!",
                forms = new List<Form>(),
                inputSurvey = new Survey
                {
                    Id = Guid.NewGuid(),
                    fields = new List<Field> {
                     new Field(Field.Kinds.String,"firstname","Please enter employee first name",null),
                     new Field(Field.Kinds.String,"lastname","Please enter employee last name",null),

                }
                },
                //assignments = new List<Assignment>(),
                criteria = new List<Criteria>(),
                destination = new Destination()

            };
            Flow template3 = new Flow
            {
                IsATemplate = true,
                ThumbnailImage = "placeholder.jpg",
                Id = new Guid("5710c736-f5b9-475f-9ef5-76529ea05fb0"),
                name = "Taxes",
                Description = "File your taxes.",                
                inputSurvey = new Survey
                {
                    Id = Guid.NewGuid(),
                    fields = new List<Field> {//0)
                     new Field(Field.Kinds.String,"firstname","Please enter Donee's first name", null   ),
                     new Field(Field.Kinds.String,"lastname","Please enter Donee's last name",null),
                     new Field(Field.Kinds.String,"street","Please enter street address",null),
                     new Field(Field.Kinds.String,"address","Enter City, State, and Country",null),
                     new Field(Field.Kinds.String,"zip","Enter Zip Code",null),
                    new Field(Field.Kinds.String,"phone","Enter Telphone number",null),

                     new Field(Field.Kinds.String,"tin1","Donee's TIN ",null),
                      new Field(Field.Kinds.String,"filerTin","Filer's TIN ",null),
                       new Field(Field.Kinds.String,"filerFirstName","Filer's first name",null),
                       new Field(Field.Kinds.String,"filerLastName","Filer's last name",null),
                        new Field(Field.Kinds.String,"filerAddress1","Street address",null),
                         new Field(Field.Kinds.String,"filerAdress2","City/town, State, Zip Code, Country",null),

                         //1
                    new Field(Field.Kinds.String,"date","Date of contribution",null),
                    //2a
                    new Field(Field.Kinds.String,"miles","Odometer mileage",null),
                    //2b
                     new Field(Field.Kinds.String,"year","Year",null),
                     //2c
                      new Field(Field.Kinds.String,"make","Make",null),
                      //2d
                       new Field(Field.Kinds.String,"model","Model",null),
                       //3
                        new Field(Field.Kinds.String,"vin","Vehicle or other Identification number ",null),
                        //4b
                         new Field(Field.Kinds.String,"saleDate","Date of Sale",null),
                         //4c
                          new Field(Field.Kinds.String,"amount","Gross proceeds from sale",null),
                          //6b
                     new Field(Field.Kinds.String,"barter","Value of goods and services provided in exchange for the vehicle",new Filter("6a", "yes")),
                            //6c
                            new Field(Field.Kinds.String,"goodsDescription", "Describe the goods and services, if any, that were provided.",new Filter("6a","yes"))
                    
                }
                },
                //assignments = new List<Assignment>(),
                criteria = new List<Criteria>{
                    new Criteria{
                      Id = Guid.NewGuid(),
                       prompt = "Did you provide goods or services in exchange for the vehicle?",
                       Category = "6a",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                           new Answer("No","no"),
                       }
                    },
                    new Criteria() {
                        Id = Guid.NewGuid(),
                       prompt = "Donee certifies that vehicle was sold in arm's length transaction to unrelated party",
                       Category = "Vehicle Transaction",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                           new Answer("No","no"),
                       }
                    },
            
                        new Criteria() {
                      Id = Guid.NewGuid(),
                       prompt = "Donee certifies that vehicle will not be transferred for money, other property, or services before completion of material improvements or significant intervening use",
                       Category = "Transfer Information",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                           new Answer("No","no"),
                       }
                
                  },  new Criteria() {
                      Id = Guid.NewGuid(),
                       prompt = "Donee certifies that vehicle is to be transferred to a needy individual for significantly below fair market value in furtherance of donee’s charitable purpose",
                       Category = "Relocation of Vehicle",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                           new Answer("No","no"),
                       }
                  },
                   new Criteria() {
                      Id = Guid.NewGuid(),
                       prompt = "Donee certifies the following detailed description of material improvements or significant intervening use and duration of use",
                       Category = "User Agreement",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                           new Answer("No","no"),
                       }
                    },
                          new Criteria() {
                      Id = Guid.NewGuid(),
                       prompt = "Describe the goods and services, if any, that were provided. If this box is checked, donee certifies that the goods and services consisted solely of intangible religious benefits.",
                       Category = "Charitable Contributions",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                       }
                    },
                     new Criteria() {
                      Id = Guid.NewGuid(),
                       prompt = "Under the law, the donor may not claim a deduction of more than $500 for this vehicle if this box is checked",
                       Category = "Contributions of Motor Vehicles, Boats and Airplanes",
                       answers = new List<Answer>
                       {
                           new Answer("Yes","yes"),
                        
                       }
                    },
                  }, 
                destination = new Destination(),
                forms = new List<Form> {
                    new Form {
                        name = "f1098c",
                        fileName = "f1098c.pdf",
                        kind = "pdf",
                        assignments = new List<Assignment> {
                            new Assignment{
                                inputField =  "firstname",
                                outputField = "topmostSubform[0].CopyA[0].TopLeftColumn[0].f1_1[0]",
                            },
                            new Assignment {
                                inputField = "tin1",
                                outputField = "topmostSubform[0].CopyA[0].TopLeftColumn[0].f1_2[0]"
                            },
                            new Assignment {
                                inputField = "filerTin",
                                outputField = "topmostSubform[0].CopyA[0].TopLeftColumn[0].f1_3[0]",
                            },
                            new Assignment {
                                inputField = "filerFirstName",
                                outputField = "topmostSubform[0].CopyA[0].TopLeftColumn[0].f1_4[0]"
                            },
                            new Assignment {
                                inputField = "filerAddress1",
                                outputField = "topmostSubform[0].CopyA[0].TopLeftColumn[0].f1_5[0]"
                            },
                            new Assignment {
                                inputField = "filerAddress2",
                                outputField = "topmostSubform[0].CopyA[0].TopLeftColumn[0].f1_7[0]"
                            },
                             //here is how to do a checkbox assignment
                            new Assignment {
                                inputField ="6a",
                                outputField = "topmostSubform[0].CopyA[0].c1_5[0]"
                            },
                            //here is the resulting filter that is dependent on the checkbox
                            new Assignment {
                                 inputField ="barter",
                                 outputField = "topmostSubform[0].CopyA[0].f1_16[0]",
                                 filter = new Filter{
                                     name = "6a",
                                     value = "yes"

                                 }
                            },
                            new Assignment {
                                inputField = "goodsDescription",
                                outputField = "topmostSubform[0].CopyA[0].f1_17[0]",
                                filter = new Filter {
                                    name = "6a",
                                    value = "yes"
                                }
                            }
                        }
                        
                    },
                    new Form {
                        name = "f1040ez",
                        fileName = "f1040ez.pdf",
                        kind = "pdf",
                        assignments = new List<Assignment> {
                            new Assignment {
                                inputField= "filerFirstName",
                                outputField = "topmostSubform[0].Page1[0].Entity[0].f1_1[0]"
                            },
                            new Assignment {
                                inputField= "filerLastName",
                                outputField = "topmostSubform[0].Page1[0].Entity[0].f1_2[0]"
                            },
                            new Assignment {
                                inputField = "filerAddress1",
                                outputField = "topmostSubform[0].Page1[0].Entity[0].f1_6[0]"
                            },
                             new Assignment {
                                inputField = "filerAddress2",
                                outputField = "topmostSubform[0].Page1[0].Entity[0].f1_8[0]"
                            }

                        }
                    }
                }
            };
            List<Flow> templates = new List<Flow>();
            templates.Add(template1);
            templates.Add(template2);
            templates.Add(template3);

            return templates;
        }
    }
}
