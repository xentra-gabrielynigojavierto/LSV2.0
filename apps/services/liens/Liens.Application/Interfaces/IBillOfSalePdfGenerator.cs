using Liens.Domain.Entities;

namespace Liens.Application.Interfaces;

public interface IBillOfSalePdfGenerator
{
    byte[] Generate(BillOfSale billOfSale);
}
