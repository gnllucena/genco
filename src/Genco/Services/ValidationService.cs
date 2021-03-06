﻿using Console.Models;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Console.Services
{
    public interface IValidationService
    {
        public bool HasErrorsOnJson(Project project);
    }

    public class ValidationService : IValidationService
    {
        private readonly ILogger<ValidationService> _logger;
        private readonly IValidator<Project> _projectValidator;
        private readonly IValidator<Entity> _entityValidator;
        private readonly IValidator<Depends> _dependsValidator;
        private readonly IValidator<Property> _propertyValidator;
        private readonly IValidator<Validation> _validationValidator;
        private readonly IValidator<PreAction> _preActionValidator;

        public ValidationService(
            ILogger<ValidationService> logger,
            IValidator<Entity> entityValidator,
            IValidator<Project> projectValidator,
            IValidator<Depends> dependsValidator,
            IValidator<Property> propertyValidator,
            IValidator<Validation> validationValidator,
            IValidator<PreAction> preActionValidator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _projectValidator = projectValidator ?? throw new ArgumentNullException(nameof(projectValidator));
            _entityValidator = entityValidator ?? throw new ArgumentNullException(nameof(entityValidator));
            _dependsValidator = dependsValidator ?? throw new ArgumentNullException(nameof(dependsValidator));
            _propertyValidator = propertyValidator ?? throw new ArgumentNullException(nameof(propertyValidator));
            _validationValidator = validationValidator ?? throw new ArgumentNullException(nameof(validationValidator));
            _preActionValidator = preActionValidator ?? throw new ArgumentNullException(nameof(preActionValidator));
        }

        public bool HasErrorsOnJson(Project project)
        {
            var validations = new List<bool>();

            _logger.LogDebug($"Project \"{project.Name}\":");

            validations.Add(Log(_projectValidator.Validate(project)));

            foreach (var entity in project.Entities)
            {
                _logger.LogDebug($"Entity \"{entity.Name}\" from \"{project.Name}\" project:");

                validations.Add(Log(_entityValidator.Validate(entity)));

                if (entity.PreInserts != null)
                {
                    validations.AddRange(PreActionsValidations(entity, entity.PreInserts));
                }

                if (entity.PreUpdates != null)
                {
                    validations.AddRange(PreActionsValidations(entity, entity.PreUpdates));
                }

                foreach (var property in entity.Properties)
                {
                    _logger.LogDebug($"Property \"{property.Name}\" from \"{entity.Name}\" entity:");

                    validations.Add(Log(_propertyValidator.Validate(property)));

                    foreach (var validation in property.Validations)
                    {
                        _logger.LogDebug($"Validation \"{validation.Type}\" from \"{property.Name}\" property:");

                        validations.Add(Log(_validationValidator.Validate(validation)));

                        _logger.LogDebug($"Dependation \"on\" ({validation.Depends.On}) and \"when\" ({validation.Depends.When}) from \"{validation.Type}\" validation:");

                        validations.Add(Log(_dependsValidator.Validate(validation.Depends)));

                        if (!string.IsNullOrWhiteSpace(validation.Depends.When) &&
                            !string.IsNullOrWhiteSpace(validation.Depends.On))
                        {
                            validations.AddRange(DependsValidations(entity, property, validation));
                        }
                    }
                }
            }

            return validations.Any(x => x == true);
        }

        private bool Log(ValidationResult result)
        {
            foreach (var error in result.Errors)
            {
                _logger.LogError(error.ErrorMessage);
            }

            return result.Errors.Any();
        }

        private List<bool> DependsValidations(Entity entity, Property property, Validation validation)
        {
            var validations = new List<bool>();

            var foundCompared = false;

            for (var i = 0; i <= entity.Properties.Count - 1; i++)
            {
                var comparedProperty = entity.Properties[i];

                if (string.Equals(validation.Depends.On, comparedProperty.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    foundCompared = true;

                    validation.Depends.On = comparedProperty.Name;

                    var whenDepends = (comparedProperty.Primitive.ToLower()) switch
                    {
                        Constants.PRIMITIVE_INT => Constants.PROPERTY_INT_DEPENDS_WHEN,
                        Constants.PRIMITIVE_DECIMAL => Constants.PROPERTY_DECIMAL_DEPENDS_WHEN,
                        Constants.PRIMITIVE_DATETIME => Constants.PROPERTY_DATETIME_DEPENDS_WHEN,
                        Constants.PRIMITIVE_STRING => Constants.PROPERTY_STRING_DEPENDS_WHEN,
                        Constants.PRIMITIVE_BOOL => Constants.PROPERTY_BOOL_DEPENDS_WHEN,
                        _ => throw new InvalidOperationException($"Primitive \"{comparedProperty.Primitive.ToLower()}\" not implemented"),
                    };

                    var foundWhen = false;

                    for (var y = 0; y <= whenDepends.Count() - 1; y++)
                    {
                        var when = whenDepends[y];

                        if (when.ToLower() == validation.Depends.When.ToLower())
                        {
                            foundWhen = true;

                            break;
                        }
                    }

                    if (!foundWhen)
                    {
                        _logger.LogError($"Depends' \"on\" ({validation.Depends.On}) and \"when\" ({validation.Depends.When}) from \"{property.Name}\" property has a \"when\" ({validation.Depends.When}) not allowed for it's \"on\" ({validation.Depends.On}) primitive ({comparedProperty.Primitive}). Allowed values: {string.Join(", ", whenDepends)}");

                        validations.Add(true);
                    }

                    break;
                }
            }

            if (!foundCompared)
            {
                _logger.LogError($"Depends' \"on\" ({validation.Depends.On}) and \"when\" ({validation.Depends.When}) from \"{property.Name}\" property has a dependency not met");

                validations.Add(true);
            }

            return validations;
        }

        private List<bool> PreActionsValidations(Entity entity, IList<PreAction> preActions)
        {
            var validations = new List<bool>();

            for (var i = 0; i <= preActions.Count() - 1; i++)
            {
                var preAction = preActions[i];
                var foundCompared = false;

                _logger.LogDebug($"PreAction \"set\" ({preAction.Set}) and \"property\" ({preAction.Property}) from \"{entity.Name}\" entity:");

                validations.Add(Log(_preActionValidator.Validate(preAction)));

                for (var y = 0; y <= entity.Properties.Count() - 1; y++)
                {
                    var property = entity.Properties[y];

                    if (string.Equals(preAction.Property, property.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        foundCompared = true;

                        preAction.Property = property.Name;

                        break;
                    }
                }

                if (!foundCompared)
                {
                    _logger.LogError($"PreAction \"set\" ({preAction.Set}) and \"property\" ({preAction.Property}) from \"{entity.Name}\" entity has a dependency not met");

                    validations.Add(true);
                }
            }

            return validations;
        }
    }
}
