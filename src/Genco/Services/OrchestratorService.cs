﻿using Console.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Console.Services
{
    public interface IOrchestratorService
    {
        Task OrchestrateAsync(Configuration configuration);
    }

    public class OrchestratorService : IOrchestratorService
    {
        private readonly ILogger<OrchestratorService> _logger;
        private readonly IValidationService _validationService;
        private readonly IFileClassService _classService;
        private readonly IFileQueryService _queryService;
        private readonly IFileRepositoryService _repositoryService;
        private readonly IFileServiceService _serviceService;
        private readonly IFileValidatorService _validatorService;
        private readonly IFileControllerService _controllerService;
        private readonly IFileService _fileService;

        public OrchestratorService(
            ILogger<OrchestratorService> logger,
            IValidationService validationService,
            IFileClassService classService,
            IFileQueryService queryService,
            IFileRepositoryService repositoryService,
            IFileServiceService serviceService,
            IFileValidatorService validatorService,
            IFileControllerService controllerService,
            IFileService fileService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _classService = classService ?? throw new ArgumentNullException(nameof(classService));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
            _serviceService = serviceService ?? throw new ArgumentNullException(nameof(serviceService));
            _validatorService = validatorService ?? throw new ArgumentNullException(nameof(validatorService));
            _controllerService = controllerService ?? throw new ArgumentNullException(nameof(controllerService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        }

        public async Task OrchestrateAsync(Configuration configuration)
        {
            _logger.LogDebug("Beginning of process");

            var project = new Project();

            try
            {
                using var stream = System.IO.File.OpenRead(configuration.File);

                project = await JsonSerializer.DeserializeAsync<Project>(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());

                return;
            }

            if (_validationService.HasErrorsOnJson(project))
            {
                return;
            }

            var files = new List<Models.File>();

            try
            {
                files.AddRange(_classService.Generate(project));
                files.AddRange(_queryService.Generate(project));
                files.AddRange(_repositoryService.Generate(project));
                files.AddRange(_validatorService.Generate(project));
                files.AddRange(_serviceService.Generate(project));
                files.AddRange(_controllerService.Generate(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something happend: ");

                return;
            }

            try
            {
                await _fileService.WriteAsync(configuration, files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something happend: ");

                return;
            }

            _logger.LogDebug("End of process");
        }
    }
}
